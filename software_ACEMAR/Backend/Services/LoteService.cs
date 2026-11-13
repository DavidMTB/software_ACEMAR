using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface ILoteService
    {

        List<LoteProducto> ObtenerLotesPorProducto(int productoId);

        List<LoteProducto> ObtenerLotesActivos(int productoId);

        Task<(bool Exito, string Error)> CrearLoteAsync(LoteProducto lote);

        Task<(bool Exito, LoteProducto? LoteUsado, decimal PrecioVenta, string Error)> ConsumarStockFifoAsync(
            int productoId, decimal cantidad);

        Task<(bool Exito, string Error)> DevolverStockALoteAsync(int loteId, decimal cantidad);

        decimal ObtenerPrecioVigenteProducto(int productoId);

        decimal CalcularStockTotal(int productoId);

        Task SincronizarStockProductoAsync(int productoId);

        List<LoteProducto> ObtenerTodosLotes(string? filtroEstado = null);

        Task<(bool Exito, string Error)> CerrarLoteAsync(int loteId, string usuario);
    }

    public class LoteService : ILoteService
    {
        public List<LoteProducto> ObtenerLotesPorProducto(int productoId)
        {
            using var ctx = new AppDbContext();
            return ctx.LotesProducto
                      .Include(l => l.Proveedor)
                      .Where(l => l.ProductoId == productoId)
                      .OrderBy(l => l.FechaIngreso)
                      .ToList();
        }

        public List<LoteProducto> ObtenerLotesActivos(int productoId)
        {
            using var ctx = new AppDbContext();
            return ctx.LotesProducto
                      .Where(l => l.ProductoId == productoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                      .OrderBy(l => l.FechaIngreso)
                      .ToList();
        }

        public async Task<(bool Exito, string Error)> CrearLoteAsync(LoteProducto lote)
        {
            if (lote.ProductoId <= 0)
                return (false, "El lote debe estar asociado a un producto válido.");
            if (lote.CantidadInicial <= 0)
                return (false, "La cantidad inicial del lote debe ser mayor a cero.");
            if (lote.PrecioVenta <= 0)
                return (false, "El precio de venta del lote debe ser mayor a cero.");

            lote.CantidadDisponible = lote.CantidadInicial;
            lote.Estado = "Activo";
            lote.FechaIngreso = lote.FechaIngreso == default ? DateTime.Now : lote.FechaIngreso;

            using var ctx = new AppDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {

                var producto = await ctx.Productos.FindAsync(lote.ProductoId);
                if (producto == null)
                    return (false, "El producto asociado al lote no existe.");

                if (producto.CuMax > 0)
                {
                    decimal stockActualTotal = CalcularStockTotalConCtx(ctx, lote.ProductoId);
                    if (stockActualTotal + lote.CantidadInicial > producto.CuMax)
                    {
                        string mensaje = $"La cantidad del lote ({lote.CantidadInicial:0.##}) excede la capacidad máxima de almacenamiento (CuMax: {producto.CuMax:0.##}). Stock actual: {stockActualTotal:0.##}.";
                        return (false, mensaje);
                    }
                }

                ctx.LotesProducto.Add(lote);
                await ctx.SaveChangesAsync();

                producto.StockActual = CalcularStockTotalConCtx(ctx, lote.ProductoId);

                var lotesActivos = await ctx.LotesProducto
                    .Where(l => l.ProductoId == lote.ProductoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                    .ToListAsync();

                if (lotesActivos.Any())
                {
                    decimal maxPrecioVenta = lotesActivos.Max(l => l.PrecioVenta);
                    producto.PrecioUnitario = maxPrecioVenta;

                    var dictPrecios = producto.ObtenerPreciosPorUnidad();
                    dictPrecios[lote.UnidadMedida] = maxPrecioVenta;
                    producto.GuardarPreciosPorUnidad(dictPrecios);
                }

                await ctx.SaveChangesAsync();

                await tx.CommitAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Error al crear el lote: {ex.Message}");
            }
        }

        public async Task<(bool Exito, LoteProducto? LoteUsado, decimal PrecioVenta, string Error)> ConsumarStockFifoAsync(
            int productoId, decimal cantidad)
        {
            if (cantidad <= 0)
                return (false, null, 0m, "La cantidad a consumir debe ser mayor a cero.");

            using var ctx = new AppDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {

                var lote = await ctx.LotesProducto
                    .Where(l => l.ProductoId == productoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                    .OrderBy(l => l.FechaIngreso)
                    .FirstOrDefaultAsync();

                if (lote == null)
                {
                    var prod = await ctx.Productos.FindAsync(productoId);
                    return (false, null, 0m, $"Sin stock disponible en lotes activos para '{prod?.Nombre ?? productoId.ToString()}'.");
                }

                if (lote.CantidadDisponible < cantidad)
                {
                    return (false, null, 0m,
                        $"Stock insuficiente en lote activo. Disponible: {lote.CantidadDisponible:0.##}, solicitado: {cantidad:0.##}.");
                }

                decimal precioVenta = lote.PrecioVenta;
                lote.CantidadDisponible -= cantidad;

                if (lote.CantidadDisponible <= 0)
                {
                    lote.CantidadDisponible = 0;
                    lote.Estado = "Agotado";
                }

                var producto = await ctx.Productos.FindAsync(productoId);
                if (producto != null)
                    producto.StockActual = CalcularStockTotalConCtx(ctx, productoId) - cantidad
                                         + (lote.Estado == "Agotado" ? 0 : 0); 

                await ctx.SaveChangesAsync();

                if (producto != null)
                    producto.StockActual = CalcularStockTotalConCtx(ctx, productoId);
                await ctx.SaveChangesAsync();

                await tx.CommitAsync();
                return (true, lote, precioVenta, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, null, 0m, $"Error al consumir stock: {ex.Message}");
            }
        }

        public async Task<(bool Exito, string Error)> DevolverStockALoteAsync(int loteId, decimal cantidad)
        {
            if (cantidad <= 0)
                return (false, "La cantidad a devolver debe ser mayor a cero.");

            using var ctx = new AppDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {
                var lote = await ctx.LotesProducto.FindAsync(loteId);
                if (lote == null)
                    return (false, "El lote especificado no existe.");

                lote.CantidadDisponible += cantidad;
                if (lote.CantidadDisponible > 0 && lote.Estado == "Agotado")
                    lote.Estado = "Activo";

                var producto = await ctx.Productos.FindAsync(lote.ProductoId);
                if (producto != null)
                    producto.StockActual += cantidad;

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Error al devolver stock: {ex.Message}");
            }
        }

        public decimal ObtenerPrecioVigenteProducto(int productoId)
        {
            using var ctx = new AppDbContext();
            var lotesActivos = ctx.LotesProducto
                          .Where(l => l.ProductoId == productoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                          .ToList();

            if (lotesActivos.Any())
                return lotesActivos.Max(l => l.PrecioVenta);

            return ctx.Productos.Where(p => p.Id == productoId)
                                .Select(p => p.PrecioUnitario)
                                .FirstOrDefault();
        }

        public decimal CalcularStockTotal(int productoId)
        {
            using var ctx = new AppDbContext();
            return CalcularStockTotalConCtx(ctx, productoId);
        }

        private static decimal CalcularStockTotalConCtx(AppDbContext ctx, int productoId)
        {
            return ctx.LotesProducto
                      .Where(l => l.ProductoId == productoId && l.Estado == "Activo")
                      .Select(l => l.CantidadDisponible)
                      .ToList()
                      .Sum();
        }

        public async Task SincronizarStockProductoAsync(int productoId)
        {
            using var ctx = new AppDbContext();
            var producto = await ctx.Productos.FindAsync(productoId);
            if (producto == null) return;

            producto.StockActual = CalcularStockTotalConCtx(ctx, productoId);
            await ctx.SaveChangesAsync();
        }

        public List<LoteProducto> ObtenerTodosLotes(string? filtroEstado = null)
        {
            using var ctx = new AppDbContext();
            var query = ctx.LotesProducto
                           .Include(l => l.Producto)
                           .Include(l => l.Proveedor)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtroEstado) && filtroEstado != "Todos")
                query = query.Where(l => l.Estado == filtroEstado);

            return query.OrderByDescending(l => l.FechaIngreso).ToList();
        }

        public async Task<(bool Exito, string Error)> CerrarLoteAsync(int loteId, string usuario)
        {
            using var ctx = new AppDbContext();
            var lote = await ctx.LotesProducto.FindAsync(loteId);
            if (lote == null) return (false, "Lote no encontrado.");
            if (lote.Estado == "Cerrado") return (false, "El lote ya está cerrado.");

            lote.Estado = "Cerrado";
            await ctx.SaveChangesAsync();

            var producto = await ctx.Productos.FindAsync(lote.ProductoId);
            if (producto != null)
            {
                producto.StockActual = CalcularStockTotalConCtx(ctx, lote.ProductoId);
                await ctx.SaveChangesAsync();
            }

            return (true, string.Empty);
        }
    }
}
