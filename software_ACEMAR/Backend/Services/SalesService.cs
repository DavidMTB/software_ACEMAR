using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface ISalesService
    {
        bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError, string? unidadMedida = null);
        Task<(bool EsValido, string MensajeError)> ValidarStockDisponibleAsync(int productoId, decimal cantidadSolicitada, string? unidadMedida = null, CancellationToken cancellationToken = default);

        decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario);

        bool ProcesarVenta(Venta venta, out string mensajeError);
        Task<(bool Exito, string MensajeError)> ProcesarVentaAsync(Venta venta, CancellationToken cancellationToken = default);

        List<Venta> ObtenerVentasRecientes(int limite = 20);
        Task<List<Venta>> ObtenerVentasRecientesAsync(int limite = 20, CancellationToken cancellationToken = default);

        List<Venta> ObtenerVentasPorFecha(DateTime desde, DateTime hasta);

        decimal ObtenerPrecioVigente(int productoId);

        decimal ObtenerDeudaCliente(int clienteCreditoId);
    }

    public class SalesService : ISalesService
    {
        public bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError, string? unidadMedida = null)
        {
            var (esValido, error) = ValidarStockDisponibleAsync(productoId, cantidadSolicitada, unidadMedida).GetAwaiter().GetResult();
            mensajeError = error;
            return esValido;
        }

        public async Task<(bool EsValido, string MensajeError)> ValidarStockDisponibleAsync(
            int productoId, decimal cantidadSolicitada, string? unidadMedida = null, CancellationToken cancellationToken = default)
        {
            if (cantidadSolicitada <= 0)
                return (false, "La cantidad solicitada debe ser mayor a cero.");

            await using var context = new AppDbContext();
            var producto = await context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken);

            if (producto == null)
                return (false, "Producto no encontrado.");

            decimal factor = producto.ObtenerFactorParaUnidad(unidadMedida);
            decimal cantidadBaseRequerida = cantidadSolicitada * factor;

            if (cantidadBaseRequerida > producto.StockActual)
                return (false, "Stock insuficiente");

            return (true, string.Empty);
        }

        public decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario)
        {

            return Math.Round(cantidad * precioUnitario, 2);
        }

        public bool ProcesarVenta(Venta venta, out string mensajeError)
        {
            var (exito, error) = ProcesarVentaAsync(venta).GetAwaiter().GetResult();
            mensajeError = error;
            return exito;
        }

        public async Task<(bool Exito, string MensajeError)> ProcesarVentaAsync(
            Venta venta, CancellationToken cancellationToken = default)
        {
            if (!venta.EsValida(out string valError))
                return (false, valError);

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {

                var productoIds = venta.Detalles.Select(d => d.ProductoId).Distinct().ToList();
                var productosDict = await context.Productos
                    .Where(p => productoIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                var nuevosDetalles = new List<DetalleVenta>();

                foreach (var item in venta.Detalles)
                {
                    item.Producto      = null;
                    item.LoteProducto  = null;

                    if (!productosDict.TryGetValue(item.ProductoId, out var producto))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return (false, $"El producto ID {item.ProductoId} no existe.");
                    }

                    if (string.IsNullOrWhiteSpace(item.UnidadMedida))
                        item.UnidadMedida = producto.UnidadMedida;

                    decimal factor = producto.ObtenerFactorParaUnidad(item.UnidadMedida);
                    decimal cantidadBaseTotal = item.Cantidad * factor;
                    decimal cantidadBasePendiente = cantidadBaseTotal;

                    var lotesActivos = await context.LotesProducto
                        .Include(l => l.Proveedor)
                        .Where(l => l.ProductoId == item.ProductoId
                                 && l.Estado == "Activo"
                                 && l.CantidadDisponible > 0)
                        .OrderBy(l => l.FechaIngreso)
                        .ToListAsync(cancellationToken);

                    if (lotesActivos.Any())
                    {
                        decimal stockTotalLotes = lotesActivos.Sum(l => l.CantidadDisponible);
                        if (cantidadBasePendiente > stockTotalLotes && cantidadBasePendiente > producto.StockActual)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return (false, $"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.StockActual:0.##} {producto.UnidadMedida}, requerido: {cantidadBaseTotal:0.##} {producto.UnidadMedida} ({item.Cantidad:0.##} {item.UnidadMedida}).");
                        }

                        foreach (var lote in lotesActivos)
                        {
                            if (cantidadBasePendiente <= 0) break;

                            decimal aConsumir = Math.Min(cantidadBasePendiente, lote.CantidadDisponible);
                            lote.CantidadDisponible -= aConsumir;
                            cantidadBasePendiente -= aConsumir;

                            if (lote.CantidadDisponible <= 0)
                            {
                                lote.CantidadDisponible = 0;
                                lote.Estado = "Agotado";
                            }

                            decimal cantPresentacionConsumida = aConsumir / factor;
                            decimal costoUnitarioPresentacion = lote.CostoCompra * factor;

                            nuevosDetalles.Add(new DetalleVenta
                            {
                                ProductoId = item.ProductoId,
                                ProductoNombre = item.ProductoNombre,
                                UnidadMedida = item.UnidadMedida,
                                Cantidad = cantPresentacionConsumida,
                                PrecioUnitario = item.PrecioUnitario,
                                CostoUnitarioCompra = costoUnitarioPresentacion,
                                LoteProductoId = lote.Id,
                                LoteProducto = lote,
                                Producto = producto
                            });
                        }

                        if (cantidadBasePendiente > 0)
                        {
                            decimal cantPresentacionConsumida = cantidadBasePendiente / factor;
                            decimal costoUnitarioPresentacion = item.PrecioUnitario * 0.70m;

                            nuevosDetalles.Add(new DetalleVenta
                            {
                                ProductoId = item.ProductoId,
                                ProductoNombre = item.ProductoNombre,
                                UnidadMedida = item.UnidadMedida,
                                Cantidad = cantPresentacionConsumida,
                                PrecioUnitario = item.PrecioUnitario,
                                CostoUnitarioCompra = costoUnitarioPresentacion,
                                LoteProductoId = null
                            });
                        }

                        producto.StockActual = Math.Max(0m, producto.StockActual - cantidadBaseTotal);
                    }
                    else
                    {

                        if (cantidadBaseTotal > producto.StockActual)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return (false, $"Stock insuficiente para '{producto.Nombre}'.");
                        }

                        producto.StockActual -= cantidadBaseTotal;

                        nuevosDetalles.Add(new DetalleVenta
                        {
                            ProductoId = item.ProductoId,
                            ProductoNombre = item.ProductoNombre,
                            UnidadMedida = item.UnidadMedida,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            CostoUnitarioCompra = item.CostoUnitarioCompra > 0 ? item.CostoUnitarioCompra : Math.Round(item.PrecioUnitario * 0.70m, 2),
                            LoteProductoId = null
                        });
                    }

                    var lotesActivosRestantes = await context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                        .ToListAsync(cancellationToken);

                    if (lotesActivosRestantes.Any())
                    {
                        producto.PrecioUnitario = lotesActivosRestantes.Max(l => l.PrecioVenta);
                    }
                }

                venta.Detalles = nuevosDetalles;

                venta.ClienteCredito = null;
                venta.Fecha = DateTime.Now;
                venta.RecalcularTotal();

                if (string.IsNullOrWhiteSpace(venta.NumeroTicket))
                {
                    var maxId = await context.Ventas.MaxAsync(v => (int?)v.Id, cancellationToken) ?? 0;
                    venta.NumeroTicket = $"TCK-{DateTime.Now:yyyyMMdd}-{(maxId + 1):D4}";
                }

                if (venta.TipoVenta == TipoVentaConsts.Credito && venta.ClienteCreditoId.HasValue)
                {
                    var cliente = await context.ClientesCredito
                        .FirstOrDefaultAsync(c => c.Id == venta.ClienteCreditoId.Value, cancellationToken);

                    if (cliente != null)
                    {
                        cliente.DeudaActual = Math.Round(
                            cliente.DeudaActual + venta.Total, 2);
                        cliente.UltimaActualizacion = DateTime.Now;
                    }
                }

                await context.Ventas.AddAsync(venta, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Error al registrar la venta: {ex.Message}");
            }
        }

        public List<Venta> ObtenerVentasRecientes(int limite = 20)
        {
            return ObtenerVentasRecientesAsync(limite).GetAwaiter().GetResult();
        }

        public async Task<List<Venta>> ObtenerVentasRecientesAsync(
            int limite = 20, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.LoteProducto)
                        .ThenInclude(l => l!.Proveedor)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.ClienteCredito)
                .OrderByDescending(v => v.Fecha)
                .Take(limite)
                .ToListAsync(cancellationToken);
        }

        public List<Venta> ObtenerVentasPorFecha(DateTime desde, DateTime hasta)
        {
            using var context = new AppDbContext();
            return context.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.LoteProducto)
                        .ThenInclude(l => l!.Proveedor)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.Fecha.Date >= desde.Date && v.Fecha.Date <= hasta.Date)
                .OrderByDescending(v => v.Fecha)
                .ToList();
        }

        public decimal ObtenerPrecioVigente(int productoId)
        {
            using var ctx = new AppDbContext();
            var lotesActivos = ctx.LotesProducto
                         .Where(l => l.ProductoId == productoId
                                  && l.Estado == "Activo"
                                  && l.CantidadDisponible > 0)
                         .ToList();

            if (lotesActivos.Any()) return lotesActivos.Max(l => l.PrecioVenta);

            return ctx.Productos
                      .Where(p => p.Id == productoId)
                      .Select(p => p.PrecioUnitario)
                      .FirstOrDefault();
        }

        public decimal ObtenerDeudaCliente(int clienteCreditoId)
        {
            using var ctx = new AppDbContext();
            return ctx.ClientesCredito
                      .Where(c => c.Id == clienteCreditoId)
                      .Select(c => c.DeudaActual)
                      .FirstOrDefault();
        }
    }
}
