using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_ACEMAR.Models
{
    /// <summary>
    /// Registro de auditoría para cambios en ventas procesadas.
    /// Toda corrección de una venta genera al menos un registro aquí.
    /// </summary>
    public class VentaAuditoria
    {
        public int Id { get; set; }

        [Required]
        public int VentaId { get; set; }

        [ForeignKey(nameof(VentaId))]
        public Venta? Venta { get; set; }

        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(100)]
        public string UsuarioModifica { get; set; } = string.Empty;

        /// <summary>Descripción legible del cambio realizado.</summary>
        [Required]
        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>Estado/valor anterior serializado (JSON o texto plano).</summary>
        [MaxLength(2000)]
        public string? ValorAnterior { get; set; }

        /// <summary>Estado/valor nuevo serializado (JSON o texto plano).</summary>
        [MaxLength(2000)]
        public string? ValorNuevo { get; set; }
    }
}
