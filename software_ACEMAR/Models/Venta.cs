using System;
using System.Collections.Generic;

namespace software_ACEMAR.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public string NumeroTicket { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public decimal Total { get; set; }
        public string UsuarioVendedor { get; set; } = string.Empty;

        // Tipo de venta para arqueo de caja (Efectivo vs Fiado)
        public string TipoVenta { get; set; } = "Efectivo"; // "Efectivo" o "Credito"
        public int? ClienteCreditoId { get; set; }
        public string? ClienteNombre { get; set; }

        public List<DetalleVenta> Detalles { get; set; } = new();
    }
}
