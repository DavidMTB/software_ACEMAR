using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_ACEMAR.Models
{

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

        [Required]
        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? ValorAnterior { get; set; }

        [MaxLength(2000)]
        public string? ValorNuevo { get; set; }
    }
}
