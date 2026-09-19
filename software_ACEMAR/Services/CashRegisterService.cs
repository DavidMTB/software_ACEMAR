using System;
using System.Collections.Generic;
using System.Linq;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public class MovimientoCaja
    {
        public DateTime FechaHora { get; set; }
        public string Tipo { get; set; } = string.Empty; // "Venta Efectivo", "Venta Fiado", "Abono Cobrado"
        public string Documento { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public bool EsIngresoFisico { get; set; }
    }

    public class ResumenCaja
    {
        public DateTime Fecha { get; set; }
        public decimal MontoApertura { get; set; }
        public decimal TotalVentasEfectivo { get; set; }
        public decimal TotalVentasCredito { get; set; }
        public decimal TotalAbonosEfectivo { get; set; }
        public decimal TotalEfectivoCaja => MontoApertura + TotalVentasEfectivo + TotalAbonosEfectivo;
        public List<MovimientoCaja> Movimientos { get; set; } = new();
    }

    public interface ICashRegisterService
    {
        ResumenCaja ObtenerResumenDelDia(DateTime fecha, decimal montoApertura);
        bool GuardarArqueoCaja(ArqueoCaja arqueo, out string error);
        List<ArqueoCaja> ObtenerHistorialArqueos();
    }

    public class CashRegisterService : ICashRegisterService
    {
        public ResumenCaja ObtenerResumenDelDia(DateTime fecha, decimal montoApertura)
        {
            var inicio = fecha.Date;
            var fin = fecha.Date.AddDays(1);

            using var context = new AppDbContext();

            // 1. Ventas del día (en memoria con AsEnumerable para evitar limitaciones de SQLite)
            var ventasDelDia = context.Ventas
                .Where(v => v.Fecha >= inicio && v.Fecha < fin)
                .AsEnumerable()
                .ToList();

            // 2. Abonos del día
            var abonosDelDia = context.AbonosCredito
                .Where(a => a.Fecha >= inicio && a.Fecha < fin)
                .AsEnumerable()
                .ToList();

            // Cargar clientes para nombres de abonos
            var clientesDict = context.ClientesCredito.ToDictionary(c => c.Id, c => c.Nombre);

            var resumen = new ResumenCaja
            {
                Fecha = fecha.Date,
                MontoApertura = montoApertura,
                TotalVentasEfectivo = Math.Round(ventasDelDia.Where(v => v.TipoVenta != "Credito").Sum(v => v.Total), 2),
                TotalVentasCredito = Math.Round(ventasDelDia.Where(v => v.TipoVenta == "Credito").Sum(v => v.Total), 2),
                TotalAbonosEfectivo = Math.Round(abonosDelDia.Sum(a => a.Monto), 2)
            };

            // Construir lista cronológica de movimientos
            var movimientos = new List<MovimientoCaja>();

            foreach (var v in ventasDelDia)
            {
                bool esCredito = v.TipoVenta == "Credito";
                movimientos.Add(new MovimientoCaja
                {
                    FechaHora = v.Fecha,
                    Tipo = esCredito ? "Venta a Crédito (Fiado)" : "Venta en Mostrador (Efectivo)",
                    Documento = v.NumeroTicket,
                    Detalle = esCredito ? $"Cliente: {v.ClienteNombre ?? "Fiado"}" : $"Atendido por: {v.UsuarioVendedor}",
                    Monto = v.Total,
                    EsIngresoFisico = !esCredito
                });
            }

            foreach (var a in abonosDelDia)
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

            resumen.Movimientos = movimientos.OrderByDescending(m => m.FechaHora).ToList();
            return resumen;
        }

        public bool GuardarArqueoCaja(ArqueoCaja arqueo, out string error)
        {
            error = string.Empty;
            try
            {
                using var context = new AppDbContext();
                arqueo.FechaHoraRegistro = DateTime.Now;
                context.ArqueosCaja.Add(arqueo);
                context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error al registrar el arqueo de caja: {ex.Message}";
                return false;
            }
        }

        public List<ArqueoCaja> ObtenerHistorialArqueos()
        {
            using var context = new AppDbContext();
            return context.ArqueosCaja
                .OrderByDescending(a => a.FechaHoraRegistro)
                .Take(30)
                .ToList();
        }
    }
}
