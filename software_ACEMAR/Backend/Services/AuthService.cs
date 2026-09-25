using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        Task<(bool Exito, string Error)> IniciarSesionAdminAsync(string usuario, string password, CancellationToken cancellationToken = default);
        void CerrarSesionAdmin();
    }

    public class AuthService : IAuthService
    {
        private Usuario _usuarioActual;

        public event Action? OnUsuarioCambiado;

        public AuthService()
        {
            // Inicializar por defecto como Administrador para acceso completo
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
            var (exito, err) = IniciarSesionAdminAsync(usuario, password).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> IniciarSesionAdminAsync(string usuario, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Debe ingresar el nombre de usuario y la contraseña.");
            }

            // Credenciales requeridas: usuario 'Gerente', password 'acemar'
            bool credencialValida = false;
            string nombreCompleto = "Gerente General ACEMAR";

            if (string.Equals(usuario.Trim(), "Gerente", StringComparison.OrdinalIgnoreCase) && password == "acemar")
            {
                credencialValida = true;
            }
            else
            {
                try
                {
                    await using var context = new AppDbContext();
                    var userDb = await context.Usuarios
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => 
                            u.NombreUsuario.ToLower() == usuario.Trim().ToLower() && 
                            u.Password == password &&
                            u.Rol == "Administrador", cancellationToken);

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
                return (false, "Credenciales incorrectas. Verifique el usuario (Gerente) y la contraseña.");
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
            return (true, string.Empty);
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
