using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace software_ACEMAR.Models
{

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

        public List<ProductoProveedor> ProductosProveedor { get; set; } = new();
    }
}
