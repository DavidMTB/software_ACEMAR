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

        /// <summary>Obtiene ventas de un rango de fechas para el Dashboard.</summary>
        List<Venta> ObtenerVentasPorFecha(DateTime desde, DateTime hasta);

        /// <summary>Obtiene el precio de venta vigente de un producto (del lote FIFO activo).</summary>
        decimal ObtenerPrecioVigente(int productoId);

        /// <summary>Obtiene la deuda actual de un ClienteCredito por su ID. Retorna 0 si no existe.</summary>
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

            // Regla INV-01 (Bloqueo de Venta): Si cantidad_base_requerida > stock_actual -> "Stock insuficiente"
            if (cantidadBaseRequerida > producto.StockActual)
                return (false, "Stock insuficiente");

            return (true, string.Empty);
        }

        public decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario)
        {
            // Regla POS-02 (Fracciones): cantidad * precio, redondeado a 2 decimales
            return Math.Round(cantidad * precioUnitario, 2);
        }

        public bool ProcesarVenta(Venta venta, out string mensajeError)
        {
            var (exito, error) = ProcesarVentaAsync(venta).GetAwaiter().GetResult();
            mensajeError = error;
            return exito;
        }

        /// <summary>
        /// Procesa una venta de forma atómica:
        /// 1. Valida el objeto Venta.
        /// 2. Consume stock de lotes activos en FIFO aplicando factor de conversión de unidades.
        /// 3. Actualiza DeudaActual si TipoVenta == Credito.
        /// 4. Genera NumeroTicket y persiste.
        /// </summary>
        public async Task<(bool Exito, string MensajeError)> ProcesarVentaAsync(
            Venta venta, CancellationToken cancellationToken = default)
        {
            if (!venta.EsValida(out string valError))
                return (false, valError);

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // ── Cargar productos involucrados (evita N+1) ────────────────────
                var productoIds = venta.Detalles.Select(d => d.ProductoId).Distinct().ToList();
                var productosDict = await context.Productos
                    .Where(p => productoIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                foreach (var item in venta.Detalles)
                {
                    item.Producto      = null;
                    item.LoteProducto  = null;

                    if (!productosDict.TryGetValue(item.ProductoId, out var producto))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return (false, $"El producto ID {item.ProductoId} no existe.");
                    }

                    // Unidad de medida por defecto si no viene del POS
                    if (string.IsNullOrWhiteSpace(item.UnidadMedida))
                        item.UnidadMedida = producto.UnidadMedida;

                    // Factor multiplicador de unidad (ej: 1 Volquetada = 4 Metros base)
                    decimal factor = producto.ObtenerFactorParaUnidad(item.UnidadMedida);
                    decimal cantidadBaseTotal = item.Cantidad * factor;
                    decimal cantidadBasePendiente = cantidadBaseTotal;

                    // ── Intentar consumo FIFO multi-lote en stock base ─────────────────
                    var lotesActivos = await context.LotesProducto
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

                            if (!item.LoteProductoId.HasValue)
                            {
                                item.LoteProductoId = lote.Id;
                                if (item.PrecioUnitario <= 0)
                                {
                                    item.PrecioUnitario = lote.PrecioVenta * factor;
                                }
                            }

                            decimal aConsumir = Math.Min(cantidadBasePendiente, lote.CantidadDisponible);
                            lote.CantidadDisponible -= aConsumir;
                            cantidadBasePendiente -= aConsumir;

                            if (lote.CantidadDisponible <= 0)
                            {
                                lote.CantidadDisponible = 0;
                                lote.Estado = "Agotado";
                            }
                        }

                        producto.StockActual = Math.Max(0m, producto.StockActual - cantidadBaseTotal);
                    }
                    else
                    {
                        // Sin lotes: verificar stock directo del producto (retrocompatibilidad)
                        if (cantidadBaseTotal > producto.StockActual)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return (false, $"Stock insuficiente para '{producto.Nombre}'.");
                        }

                        producto.StockActual -= cantidadBaseTotal;
                    }

                    // Sincronizar el PrecioUnitario del producto con el precio del siguiente lote FIFO activo
                    var siguienteLoteActivo = await context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                        .OrderBy(l => l.FechaIngreso)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (siguienteLoteActivo != null)
                    {
                        producto.PrecioUnitario = siguienteLoteActivo.PrecioVenta;
                    }
                }

                // ── Fecha y ticket ───────────────────────────────────────────────
                venta.ClienteCredito = null;
                venta.Fecha = DateTime.Now;
                venta.RecalcularTotal();

                if (string.IsNullOrWhiteSpace(venta.NumeroTicket))
                {
                    var maxId = await context.Ventas.MaxAsync(v => (int?)v.Id, cancellationToken) ?? 0;
                    venta.NumeroTicket = $"TCK-{DateTime.Now:yyyyMMdd}-{(maxId + 1):D4}";
                }

                // ── FIX BUG FIADO: actualizar DeudaActual del cliente directamente ─
                // En lugar de llamar a CreditService.RegistrarNuevoFiado (que creaba
                // una Venta duplicada), actualizamos la deuda aquí dentro de la misma
                // transacción. El PosViewModel NO debe llamar a RegistrarNuevoFiado.
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
                .Where(v => v.Fecha.Date >= desde.Date && v.Fecha.Date <= hasta.Date)
                .OrderByDescending(v => v.Fecha)
                .ToList();
        }

        public decimal ObtenerPrecioVigente(int productoId)
        {
            using var ctx = new AppDbContext();
            var lote = ctx.LotesProducto
                         .Where(l => l.ProductoId == productoId
                                  && l.Estado == "Activo"
                                  && l.CantidadDisponible > 0)
                         .OrderBy(l => l.FechaIngreso)
                         .FirstOrDefault();

            if (lote != null) return lote.PrecioVenta;

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
