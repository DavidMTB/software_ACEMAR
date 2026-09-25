using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace software_ACEMAR.Models
{
    public static class TipoVentaConsts
    {
        public const string Efectivo = "Efectivo";
        public const string Credito = "Credito";
    }

    /// <summary>
    /// Entidad de Dominio que representa un comprobante / ticket de venta en el Punto de Venta (POS).
    /// </summary>
    public class Venta
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string NumeroTicket { get; set; } = string.Empty;

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Precision(18, 2)]
        public decimal Total { get; set; }

        [Required]
        [MaxLength(100)]
        public string UsuarioVendedor { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de venta para clasificación y arqueo de caja ("Efectivo" vs "Credito").
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string TipoVenta { get; set; } = TipoVentaConsts.Efectivo;

        public int? ClienteCreditoId { get; set; }

        [MaxLength(150)]
        public string? ClienteNombre { get; set; }

        // Propiedad de navegación relacional en EF Core
        [ForeignKey(nameof(ClienteCreditoId))]
        public ClienteCredito? ClienteCredito { get; set; }

        // Colección de renglones / productos vendidos
        public List<DetalleVenta> Detalles { get; set; } = new();

        /// <summary>
        /// Recalcula el total de la venta sumando los subtotales redondeados de sus detalles.
        /// </summary>
        public decimal RecalcularTotal()
        {
            Total = Math.Round(Detalles?.Sum(d => d.Subtotal) ?? 0m, 2);
            return Total;
        }

        /// <summary>
        /// Revisa la validez del objeto Venta antes de persistir.
        /// </summary>
        public bool EsValida(out string error)
        {
            error = string.Empty;
            if (Detalles == null || !Detalles.Any())
            {
                error = "La venta debe contener al menos un producto.";
                return false;
            }

            if (Detalles.Any(d => d.Cantidad <= 0))
            {
                error = "La cantidad de cada ítem en la venta debe ser mayor a cero.";
                return false;
            }

            if (Detalles.Any(d => d.PrecioUnitario < 0))
            {
                error = "El precio unitario no puede ser negativo.";
                return false;
            }

            return true;
        }
    }
}
