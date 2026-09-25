using System;

namespace software_ACEMAR.Models
{
    public class AbonoCredito
    {
        public int Id { get; set; }
        public int ClienteCreditoId { get; set; }
        public ClienteCredito? ClienteCredito { get; set; }

        public decimal Monto { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Observaciones { get; set; } = string.Empty;
        public string UsuarioRegistra { get; set; } = string.Empty;
    }
}
