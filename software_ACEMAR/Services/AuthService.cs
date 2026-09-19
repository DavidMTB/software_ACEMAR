using System;
using System.Linq;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IAuthService
    {
        Usuario UsuarioActual { get; }
        event Action? OnUsuarioCambiado;
        void SetUsuario(Usuario usuario);
        void CambiarRol(string rol);
        bool PuedeAccederCreditos(out string mensajeError);
        bool IniciarSesionAdmin(string usuario, string password, out string error);
        void CerrarSesionAdmin();
    }

    public class AuthService : IAuthService
    {
        private Usuario _usuarioActual;

        public event Action? OnUsuarioCambiado;

        public AuthService()
        {
            // Inicializar por defecto como Vendedor para que se note la autenticación, o Admin
            _usuarioActual = new Usuario
            {
                Id = 1,
                NombreUsuario = "Gerente",
                NombreCompleto = "Gerente General ACEMAR",
                Password = "acemar",
                Rol = "Administrador"
            };
        }

        public Usuario UsuarioActual => _usuarioActual;

        public void SetUsuario(Usuario usuario)
        {
            _usuarioActual = usuario;
            OnUsuarioCambiado?.Invoke();
        }

        public void CambiarRol(string nuevoRol)
        {
            _usuarioActual.Rol = nuevoRol;
            OnUsuarioCambiado?.Invoke();
        }

        public bool IniciarSesionAdmin(string usuario, string password, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                error = "Debe ingresar el nombre de usuario y la contraseña.";
                return false;
            }

            // Credenciales requeridas por el cliente:
            // nombre: Gerente, password: acemar
            bool credencialValida = false;
            string nombreCompleto = "Gerente General ACEMAR";

            if (string.Equals(usuario.Trim(), "Gerente", StringComparison.OrdinalIgnoreCase) && password == "acemar")
            {
                credencialValida = true;
            }
            else
            {
                // También verificar contra la base de datos local SQLite
                try
                {
                    using var context = new AppDbContext();
                    var userDb = context.Usuarios.FirstOrDefault(u => 
                        u.NombreUsuario.ToLower() == usuario.Trim().ToLower() && 
                        u.Password == password &&
                        u.Rol == "Administrador");

                    if (userDb != null)
                    {
                        credencialValida = true;
                        nombreCompleto = userDb.NombreCompleto;
                    }
                }
                catch
                {
                    // Fallback a validación directa
                }
            }

            if (!credencialValida)
            {
                error = "Credenciales incorrectas. Verifique el usuario (Gerente) y la contraseña.";
                return false;
            }

            _usuarioActual = new Usuario
            {
                Id = 1,
                NombreUsuario = "Gerente",
                NombreCompleto = nombreCompleto,
                Password = "acemar",
                Rol = "Administrador"
            };

            OnUsuarioCambiado?.Invoke();
            return true;
        }

        public void CerrarSesionAdmin()
        {
            _usuarioActual = new Usuario
            {
                Id = 2,
                NombreUsuario = "vendedor",
                NombreCompleto = "Carlos Vendedor",
                Password = "123",
                Rol = "Vendedor"
            };
            OnUsuarioCambiado?.Invoke();
        }

        public bool PuedeAccederCreditos(out string mensajeError)
        {
            // Regla CRE-01 (Seguridad Fiados):
            // Si un usuario con rol "Vendedor" intenta abrir la vista de créditos o procesar uno,
            // bloquear acceso con el mensaje: "Permisos insuficientes. Contacte al Administrador".
            if (string.Equals(_usuarioActual.Rol, "Vendedor", StringComparison.OrdinalIgnoreCase))
            {
                mensajeError = "Permisos insuficientes. Contacte al Administrador";
                return false;
            }

            mensajeError = string.Empty;
            return true;
        }
    }
}
