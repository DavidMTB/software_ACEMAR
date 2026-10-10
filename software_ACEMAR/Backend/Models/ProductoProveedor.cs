using System.ComponentModel.DataAnnotations.Schema;

namespace software_ACEMAR.Models
{
    /// <summary>
    /// Relación muchos-a-muchos entre Producto y Proveedor.
    /// Permite saber qué proveedores suministran un producto y cuál es el habitual.
    /// </summary>
    public class ProductoProveedor
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }

        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        public int ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedor? Proveedor { get; set; }

        /// <summary>Indica si este proveedor es el habitual/principal para este producto.</summary>
        public bool EsHabitual { get; set; } = false;
    }
}
