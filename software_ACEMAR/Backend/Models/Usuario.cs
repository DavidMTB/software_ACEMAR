namespace software_ACEMAR.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Password { get; set; } = "acemar";
        public string Rol { get; set; } = "Vendedor"; // "Administrador" o "Vendedor"
    }
}
