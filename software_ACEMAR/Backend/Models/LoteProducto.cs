using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace software_ACEMAR.Models
{
    /// <summary>
    /// Representa un lote de compra de un producto.
    /// Cada lote puede tener su propio costo y precio de venta independientes.
    /// La venta siempre consume stock del lote referenciado y aplica su precio.
    /// </summary>
    public class LoteProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }

        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        /// <summary>Fecha en que ingresó este lote al almacén.</summary>
        public DateTime FechaIngreso { get; set; } = DateTime.Now;

        /// <summary>Cantidad original con la que entró el lote.</summary>
        [Precision(18, 2)]
        public decimal CantidadInicial { get; set; }

        /// <summary>Cantidad actual disponible para vender de este lote.</summary>
        [Precision(18, 2)]
        public decimal CantidadDisponible { get; set; }

        /// <summary>Costo de compra por unidad para este lote (para cálculo de margen).</summary>
        [Precision(18, 2)]
        public decimal CostoCompra { get; set; }

        /// <summary>Precio de venta unitario de este lote. Independiente de otros lotes.</summary>
        [Precision(18, 2)]
        public decimal PrecioVenta { get; set; }

        /// <summary>Unidad de medida principal de este lote.</summary>
        [MaxLength(50)]
        public string UnidadMedida { get; set; } = "Unidad";

        [MaxLength(300)]
        public string? Observaciones { get; set; }

        /// <summary>"Activo" = tiene stock disponible. "Agotado" = CantidadDisponible llegó a 0. "Cerrado" = cerrado manualmente.</summary>
        [MaxLength(20)]
        public string Estado { get; set; } = "Activo";

        // Proveedor asociado a este lote (de quién se compró)
        public int? ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedor? Proveedor { get; set; }

        // Propiedades calculadas (no persistidas en BD)
        [NotMapped]
        public bool EstaAgotado => CantidadDisponible <= 0;

        [NotMapped]
        public decimal PorcentajeDisponible =>
            CantidadInicial > 0 ? Math.Round((CantidadDisponible / CantidadInicial) * 100, 1) : 0;

        [NotMapped]
        public decimal MargenBruto => PrecioVenta - CostoCompra;

        [NotMapped]
        public decimal MargenPorcentaje =>
            CostoCompra > 0 ? Math.Round((MargenBruto / CostoCompra) * 100, 1) : 0;

        [NotMapped]
        public string ProveedorNombreDisplay
        {
            get
            {
                if (Proveedor != null && !string.IsNullOrWhiteSpace(Proveedor.Nombre))
                    return Proveedor.Nombre;

                if (!string.IsNullOrWhiteSpace(Observaciones) && Observaciones.Contains("Proveedor:"))
                {
                    var partes = Observaciones.Split('|');
                    var provPart = partes.FirstOrDefault(p => p.Trim().StartsWith("Proveedor:", StringComparison.OrdinalIgnoreCase));
                    if (provPart != null)
                    {
                        string val = provPart.Replace("Proveedor:", "", StringComparison.OrdinalIgnoreCase).Trim();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }

                return "Proveedor General";
            }
        }
    }
}
