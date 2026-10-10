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
        /// <summary>Obtiene todos los lotes de un producto, ordenados por fecha (FIFO).</summary>
        List<LoteProducto> ObtenerLotesPorProducto(int productoId);

        /// <summary>Obtiene solo los lotes activos (con stock > 0) de un producto, ordenados por FIFO.</summary>
        List<LoteProducto> ObtenerLotesActivos(int productoId);

        /// <summary>
        /// Registra un nuevo lote de compra para un producto.
        /// Actualiza automáticamente el StockActual del Producto.
        /// </summary>
        Task<(bool Exito, string Error)> CrearLoteAsync(LoteProducto lote);

        /// <summary>
        /// Consume cantidad de los lotes de un producto en orden FIFO.
        /// Devuelve el lote consumido (o el primero si se consumió parcialmente).
        /// </summary>
        Task<(bool Exito, LoteProducto? LoteUsado, decimal PrecioVenta, string Error)> ConsumarStockFifoAsync(
            int productoId, decimal cantidad);

        /// <summary>
        /// Devuelve stock a un lote específico (usado al revertir una venta).
        /// </summary>
        Task<(bool Exito, string Error)> DevolverStockALoteAsync(int loteId, decimal cantidad);

        /// <summary>
        /// Obtiene el precio de venta vigente de un producto (del lote FIFO activo).
        /// Si no tiene lotes, devuelve el PrecioUnitario del Producto.
        /// </summary>
        decimal ObtenerPrecioVigenteProducto(int productoId);

        /// <summary>Calcula el stock total disponible de un producto sumando sus lotes activos.</summary>
        decimal CalcularStockTotal(int productoId);

        /// <summary>Recalcula y sincroniza el StockActual del Producto con la suma de sus lotes.</summary>
        Task SincronizarStockProductoAsync(int productoId);

        /// <summary>Obtiene todos los lotes con su info de producto, para la vista de gestión.</summary>
        List<LoteProducto> ObtenerTodosLotes(string? filtroEstado = null);

        /// <summary>Cierra manualmente un lote (Estado = "Cerrado").</summary>
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
                // Verificar que el producto existe
                var producto = await ctx.Productos.FindAsync(lote.ProductoId);
                if (producto == null)
                    return (false, "El producto asociado al lote no existe.");

                // Verificar CuMax (si tiene CuMax definido)
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

                // Actualizar StockActual del producto
                producto.StockActual = CalcularStockTotalConCtx(ctx, lote.ProductoId);

                // Sincronizar el PrecioUnitario del producto con el precio del lote FIFO activo
                var loteActivoVigente = await ctx.LotesProducto
                    .Where(l => l.ProductoId == lote.ProductoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                    .OrderBy(l => l.FechaIngreso)
                    .FirstOrDefaultAsync();

                if (loteActivoVigente != null)
                {
                    producto.PrecioUnitario = loteActivoVigente.PrecioVenta;

                    var dictPrecios = producto.ObtenerPreciosPorUnidad();
                    dictPrecios[lote.UnidadMedida] = loteActivoVigente.PrecioVenta;
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
                // FIFO: tomar lote más antiguo con stock disponible
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

                // Recalcular stock del producto
                var producto = await ctx.Productos.FindAsync(productoId);
                if (producto != null)
                    producto.StockActual = CalcularStockTotalConCtx(ctx, productoId) - cantidad
                                         + (lote.Estado == "Agotado" ? 0 : 0); // ya descontado

                await ctx.SaveChangesAsync();

                // Recalculo final correcto
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
            var lote = ctx.LotesProducto
                          .Where(l => l.ProductoId == productoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                          .OrderBy(l => l.FechaIngreso)
                          .FirstOrDefault();

            if (lote != null)
                return lote.PrecioVenta;

            // Fallback: precio del producto
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

            // Recalcular stock del producto
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
