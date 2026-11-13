using System.ComponentModel.DataAnnotations.Schema;

namespace software_ACEMAR.Models
{

    public class ProductoProveedor
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }

        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        public int ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedor? Proveedor { get; set; }

        public bool EsHabitual { get; set; } = false;
    }
}
