using System;
using System.Collections.Generic;

namespace software_ACEMAR.Models
{
    public class Proforma
    {
        public int Id { get; set; }
        public string NumeroProforma { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string ClienteNombre { get; set; } = "Cliente Mostrador";
        public string ClienteDocumento { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Estado { get; set; } = "Pendiente"; // "Pendiente", "Convertida", "Anulada"

        public List<DetalleProforma> Detalles { get; set; } = new();
    }
}
