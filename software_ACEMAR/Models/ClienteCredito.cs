using System;
using System.Collections.Generic;

namespace software_ACEMAR.Models
{
    public class ClienteCredito
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty; // DNI o RUC
        public string Telefono { get; set; } = string.Empty;
        public decimal LimiteCredito { get; set; }
        public decimal DeudaActual { get; set; }
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        public List<AbonoCredito> Abonos { get; set; } = new();

        public decimal CreditoDisponible => Math.Max(0, LimiteCredito - DeudaActual);
    }
}
