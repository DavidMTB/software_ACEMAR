using System;

namespace software_ACEMAR.Models
{
    public class ArqueoCaja
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;
        public DateTime FechaHoraRegistro { get; set; } = DateTime.Now;
        public decimal MontoApertura { get; set; }
        public decimal TotalVentasEfectivo { get; set; }
        public decimal TotalVentasCredito { get; set; }
        public decimal TotalAbonosEfectivo { get; set; }
        public decimal TotalEfectivoEnCaja => MontoApertura + TotalVentasEfectivo + TotalAbonosEfectivo;
        public string Estado { get; set; } = "Cerrada"; // "Abierta", "Cerrada"
        public string Observaciones { get; set; } = string.Empty;
        public string UsuarioResponsable { get; set; } = string.Empty;
    }
}
