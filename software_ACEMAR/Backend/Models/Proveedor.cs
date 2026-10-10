using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace software_ACEMAR.Models
{
    /// <summary>
    /// Representa un proveedor de productos. Permite identificar a quién contactar
    /// para la reposición de stock de un producto determinado.
    /// </summary>
    public class Proveedor
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Contacto { get; set; }

        [MaxLength(30)]
        public string? Telefono { get; set; }

        [MaxLength(30)]
        public string? WhatsApp { get; set; }

        [MaxLength(150)]
        public string? Correo { get; set; }

        [MaxLength(300)]
        public string? Direccion { get; set; }

        [MaxLength(500)]
        public string? Observaciones { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        /// <summary>Relación con productos que suministra este proveedor.</summary>
        public List<ProductoProveedor> ProductosProveedor { get; set; } = new();
    }
}
