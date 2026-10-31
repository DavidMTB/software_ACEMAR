using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_ACEMAR.Models
{
    public class DetalleVenta
    {
        public int Id { get; set; }

        public int VentaId { get; set; }
        public Venta? Venta { get; set; }

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        /// <summary>
        /// Referencia al lote del cual salió este ítem.
        /// Null para ventas anteriores a la implementación de lotes.
        /// </summary>
        public int? LoteProductoId { get; set; }

        [ForeignKey(nameof(LoteProductoId))]
        public LoteProducto? LoteProducto { get; set; }

        [Required]
        [MaxLength(200)]
        public string ProductoNombre { get; set; } = string.Empty;

        /// <summary>Unidad de medida utilizada en esta línea de venta (ej: "Lata", "Saco", "Unidad").</summary>
        [MaxLength(50)]
        public string UnidadMedida { get; set; } = "Unidad";

        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// Costo de compra unitario real para esta unidad de medida (para cálculo exacto de utilidad bruta FIFO).
        /// </summary>
        public decimal CostoUnitarioCompra { get; set; }

        /// <summary>
        /// Subtotal calculado. Es NotMapped pero se almacena el valor al crear la venta
        /// a través de PrecioUnitario * Cantidad. No se persiste como columna independiente.
        /// </summary>
        [NotMapped]
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);

        [NotMapped]
        public string ProductoCodigo => Producto?.Codigo ?? (ProductoId > 0 ? $"PRD-{ProductoId:D3}" : "-");

        [NotMapped]
        public string LoteDisplay => LoteProducto != null ? $"Lote #{LoteProducto.Id:D4}" : (LoteProductoId.HasValue ? $"Lote #{LoteProductoId.Value:D4}" : "Sin Lote");

        [NotMapped]
        public string ProveedorDisplay
        {
            get
            {
                if (LoteProducto?.Proveedor != null && !string.IsNullOrWhiteSpace(LoteProducto.Proveedor.Nombre))
                    return LoteProducto.Proveedor.Nombre;

                if (LoteProducto != null)
                    return LoteProducto.ProveedorNombreDisplay;

                return "Proveedor General";
            }
        }
    }
}
