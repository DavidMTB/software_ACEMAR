using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace software_ACEMAR.Models
{

    public class LoteProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }

        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        public DateTime FechaIngreso { get; set; } = DateTime.Now;

        [Precision(18, 2)]
        public decimal CantidadInicial { get; set; }

        [Precision(18, 2)]
        public decimal CantidadDisponible { get; set; }

        [Precision(18, 2)]
        public decimal CostoCompra { get; set; }

        [Precision(18, 2)]
        public decimal PrecioVenta { get; set; }

        [MaxLength(50)]
        public string UnidadMedida { get; set; } = "Unidad";

        [MaxLength(300)]
        public string? Observaciones { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; } = "Activo";

        public int? ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedor? Proveedor { get; set; }

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
