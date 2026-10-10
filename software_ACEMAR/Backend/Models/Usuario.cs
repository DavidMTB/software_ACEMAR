namespace software_ACEMAR.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Password { get; set; } = "acemar";
        public string Rol { get; set; } = "Vendedor"; // "Administrador" o "Vendedor"
        public string MostradorAsignado { get; set; } = "Mostrador 01"; // "Mostrador 01", "Mostrador 02", "Mostrador 03", etc.
    }
}
