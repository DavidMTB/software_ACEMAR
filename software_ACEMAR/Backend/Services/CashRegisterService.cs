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
    public class MovimientoCaja
    {
        public DateTime FechaHora { get; set; }
        public string Tipo { get; set; } = string.Empty; // "Venta Efectivo", "Venta Fiado", "Abono Cobrado", "Gasto Lote"
        public string Documento { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public bool EsIngresoFisico { get; set; }
    }

    public class ResumenCaja
    {
        public DateTime Fecha { get; set; }
        public DateTime? FechaHasta { get; set; }
        public decimal MontoApertura { get; set; }
        public decimal TotalVentasEfectivo { get; set; }
        public decimal TotalVentasDigital { get; set; }
        public decimal TotalVentasCredito { get; set; }
        public decimal TotalAbonosEfectivo { get; set; }
        public decimal TotalGastosLotes { get; set; }
        public decimal TotalCostoVentas { get; set; }
        public decimal UtilidadBruta => (TotalVentasEfectivo + TotalVentasDigital + TotalVentasCredito) - TotalCostoVentas;
        public decimal TotalEfectivoCaja => MontoApertura + TotalVentasEfectivo + TotalAbonosEfectivo - TotalGastosLotes;
        public List<MovimientoCaja> Movimientos { get; set; } = new();
    }

    public interface ICashRegisterService
    {
        ResumenCaja ObtenerResumenDelDia(DateTime fecha, decimal montoApertura);
        Task<ResumenCaja> ObtenerResumenDelDiaAsync(DateTime fecha, decimal montoApertura, CancellationToken cancellationToken = default);

        ResumenCaja ObtenerResumenPorRango(DateTime fechaDesde, DateTime fechaHasta, decimal montoApertura);
        Task<ResumenCaja> ObtenerResumenPorRangoAsync(DateTime fechaDesde, DateTime fechaHasta, decimal montoApertura, CancellationToken cancellationToken = default);

        bool GuardarArqueoCaja(ArqueoCaja arqueo, out string error);
        Task<(bool Exito, string Error)> GuardarArqueoCajaAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default);

        List<ArqueoCaja> ObtenerHistorialArqueos();
        Task<List<ArqueoCaja>> ObtenerHistorialArqueosAsync(CancellationToken cancellationToken = default);
    }

    public class CashRegisterService : ICashRegisterService
    {
        public ResumenCaja ObtenerResumenDelDia(DateTime fecha, decimal montoApertura)
        {
            return ObtenerResumenPorRango(fecha.Date, fecha.Date, montoApertura);
        }

        public async Task<ResumenCaja> ObtenerResumenDelDiaAsync(DateTime fecha, decimal montoApertura, CancellationToken cancellationToken = default)
        {
            return await ObtenerResumenPorRangoAsync(fecha.Date, fecha.Date, montoApertura, cancellationToken);
        }

        public ResumenCaja ObtenerResumenPorRango(DateTime fechaDesde, DateTime fechaHasta, decimal montoApertura)
        {
            return ObtenerResumenPorRangoAsync(fechaDesde, fechaHasta, montoApertura).GetAwaiter().GetResult();
        }

        public async Task<ResumenCaja> ObtenerResumenPorRangoAsync(DateTime fechaDesde, DateTime fechaHasta, decimal montoApertura, CancellationToken cancellationToken = default)
        {
            var inicio = fechaDesde.Date;
            var fin = fechaHasta.Date.AddDays(1);

            await using var context = new AppDbContext();

            // 1. Ventas del rango con sus detalles
            var ventasDelRango = await context.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                .Where(v => v.Fecha >= inicio && v.Fecha < fin)
                .ToListAsync(cancellationToken);

            // 2. Abonos del rango
            var abonosDelRango = await context.AbonosCredito
                .AsNoTracking()
                .Where(a => a.Fecha >= inicio && a.Fecha < fin)
                .ToListAsync(cancellationToken);

            // 3. Nuevos lotes de compra en el rango (Gastos de inversión)
            var lotesDelRango = await context.LotesProducto
                .AsNoTracking()
                .Include(l => l.Producto)
                .Where(l => l.FechaIngreso >= inicio && l.FechaIngreso < fin)
                .ToListAsync(cancellationToken);

            // Cargar diccionario de clientes para nombres de abonos
            var clientesDict = await context.ClientesCredito
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);

            // Calcular Costo de Ventas (COGS)
            decimal costoVentasTotal = 0;
            foreach (var v in ventasDelRango)
            {
                if (v.Detalles != null)
                {
                    foreach (var d in v.Detalles)
                    {
                        decimal costoUnit = d.PrecioUnitario * 0.70m;
                        if (d.LoteProductoId.HasValue)
                        {
                            var lote = await context.LotesProducto.AsNoTracking().FirstOrDefaultAsync(l => l.Id == d.LoteProductoId.Value, cancellationToken);
                            if (lote != null && lote.CostoCompra > 0) costoUnit = lote.CostoCompra;
                        }
                        costoVentasTotal += (d.Cantidad * costoUnit);
                    }
                }
            }

            decimal totalGastosLotes = lotesDelRango.Sum(l => l.CantidadInicial * l.CostoCompra);

            var resumen = new ResumenCaja
            {
                Fecha = fechaDesde.Date,
                FechaHasta = fechaHasta.Date,
                MontoApertura = montoApertura,
                TotalVentasEfectivo = Math.Round(ventasDelRango.Where(v => v.TipoVenta == TipoVentaConsts.Efectivo).Sum(v => v.Total), 2),
                TotalVentasDigital = Math.Round(ventasDelRango.Where(v => v.TipoVenta == "Digital" || v.TipoVenta == "Yape" || v.TipoVenta == "Plin" || v.TipoVenta == "Transferencia").Sum(v => v.Total), 2),
                TotalVentasCredito = Math.Round(ventasDelRango.Where(v => v.TipoVenta == TipoVentaConsts.Credito).Sum(v => v.Total), 2),
                TotalAbonosEfectivo = Math.Round(abonosDelRango.Sum(a => a.Monto), 2),
                TotalGastosLotes = Math.Round(totalGastosLotes, 2),
                TotalCostoVentas = Math.Round(costoVentasTotal, 2)
            };

            // Construir lista cronológica de movimientos
            var movimientos = new List<MovimientoCaja>();

            foreach (var v in ventasDelRango)
            {
                bool esCredito = v.TipoVenta == TipoVentaConsts.Credito;
                movimientos.Add(new MovimientoCaja
                {
                    FechaHora = v.Fecha,
                    Tipo = esCredito ? "Venta a Crédito (Fiado)" : $"Venta {v.TipoVenta}",
                    Documento = v.NumeroTicket,
                    Detalle = esCredito ? $"Cliente: {v.ClienteNombre ?? "Fiado"}" : $"Atendido por: {v.UsuarioVendedor}",
                    Monto = v.Total,
                    EsIngresoFisico = !esCredito
                });
            }

            foreach (var a in abonosDelRango)
            {
                string nombreCliente = clientesDict.TryGetValue(a.ClienteCreditoId, out var nom) ? nom : "Cliente";
                movimientos.Add(new MovimientoCaja
                {
                    FechaHora = a.Fecha,
                    Tipo = "Abono de Crédito (Efectivo)",
                    Documento = $"ABN-{a.Id:D4}",
                    Detalle = $"{nombreCliente} ({a.Observaciones})",
                    Monto = a.Monto,
                    EsIngresoFisico = true
                });
            }

            foreach (var l in lotesDelRango)
            {
                string nomProd = l.Producto?.Nombre ?? $"Producto #{l.ProductoId}";
                decimal gastoMonto = l.CantidadInicial * l.CostoCompra;
                movimientos.Add(new MovimientoCaja
                {
                    FechaHora = l.FechaIngreso,
                    Tipo = "Gasto Compra Lote",
                    Documento = $"LOT-{l.Id:D4}",
                    Detalle = $"Lote {nomProd} ({l.CantidadInicial:0.##} {l.UnidadMedida} @ S/ {l.CostoCompra:N2}/u)",
                    Monto = gastoMonto,
                    EsIngresoFisico = false
                });
            }

            resumen.Movimientos = movimientos.OrderByDescending(m => m.FechaHora).ToList();
            return resumen;
        }

        public bool GuardarArqueoCaja(ArqueoCaja arqueo, out string error)
        {
            var (exito, err) = GuardarArqueoCajaAsync(arqueo).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> GuardarArqueoCajaAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = new AppDbContext();
                arqueo.FechaHoraRegistro = DateTime.Now;
                await context.ArqueosCaja.AddAsync(arqueo, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Error al registrar el arqueo de caja: {ex.Message}");
            }
        }

        public List<ArqueoCaja> ObtenerHistorialArqueos()
        {
            return ObtenerHistorialArqueosAsync().GetAwaiter().GetResult();
        }

        public async Task<List<ArqueoCaja>> ObtenerHistorialArqueosAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.ArqueosCaja
                .AsNoTracking()
                .OrderByDescending(a => a.FechaHoraRegistro)
                .Take(30)
                .ToListAsync(cancellationToken);
        }
    }
}
