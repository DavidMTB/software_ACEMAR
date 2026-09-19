using System;

namespace software_ACEMAR.Models
{
    public class DetalleProforma
    {
        public int Id { get; set; }
        public int ProformaId { get; set; }
        public Proforma? Proforma { get; set; }

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public string ProductoNombre { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
    }
}
