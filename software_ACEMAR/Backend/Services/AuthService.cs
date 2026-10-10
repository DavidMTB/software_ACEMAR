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
        bool IniciarSesion(string usuario, string password, out string error);
        Task<(bool Exito, string Error)> IniciarSesionAsync(string usuario, string password, CancellationToken cancellationToken = default);
        bool IniciarSesionAdmin(string usuario, string password, out string error);
        Task<(bool Exito, string Error)> IniciarSesionAdminAsync(string usuario, string password, CancellationToken cancellationToken = default);
        void CerrarSesionAdmin();
        bool CerrarSesionAdmin(out bool requiereSeleccionarVendedor);
        void CerrarSesion();
    }

    public class AuthService : IAuthService
    {
        private Usuario _usuarioActual;
        private Usuario? _usuarioVendedorPrevio;

        public event Action? OnUsuarioCambiado;

        public AuthService()
        {
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
            if (string.Equals(usuario.Rol, "Vendedor", StringComparison.OrdinalIgnoreCase))
            {
                _usuarioVendedorPrevio = usuario;
            }
            OnUsuarioCambiado?.Invoke();
        }

        public void CambiarRol(string nuevoRol)
        {
            _usuarioActual.Rol = nuevoRol;
            OnUsuarioCambiado?.Invoke();
        }

        public bool IniciarSesion(string usuario, string password, out string error)
        {
            var (exito, err) = IniciarSesionAsync(usuario, password).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> IniciarSesionAsync(string usuario, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Debe ingresar el usuario y la contraseña.");
            }

            try
            {
                await using var context = new AppDbContext();
                string userLower = usuario.Trim().ToLower();
                string passTrim = password.Trim();

                var userDb = await context.Usuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.NombreUsuario.ToLower() == userLower && u.Password == passTrim, cancellationToken);

                if (userDb == null)
                {
                    return (false, "Usuario o contraseña incorrectos. Verifique sus credenciales.");
                }

                _usuarioActual = userDb;
                if (string.Equals(userDb.Rol, "Vendedor", StringComparison.OrdinalIgnoreCase))
                {
                    _usuarioVendedorPrevio = userDb;
                }

                OnUsuarioCambiado?.Invoke();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Error de autenticación: {ex.Message}");
            }
        }

        public void CerrarSesion()
        {
            _usuarioVendedorPrevio = null;
            _usuarioActual = new Usuario
            {
                Id = 0,
                NombreUsuario = "Invitado",
                NombreCompleto = "No Autenticado",
                Password = "",
                Rol = "Vendedor"
            };
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

            bool credencialValida = false;
            string nombreCompleto = "Gerente General ACEMAR";
            Usuario? adminDb = null;

            if (string.Equals(usuario.Trim(), "Gerente", StringComparison.OrdinalIgnoreCase) && password == "acemar")
            {
                credencialValida = true;
            }
            else
            {
                try
                {
                    await using var context = new AppDbContext();
                    adminDb = await context.Usuarios
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => 
                            u.NombreUsuario.ToLower() == usuario.Trim().ToLower() && 
                            u.Password == password &&
                            u.Rol == "Administrador", cancellationToken);

                    if (adminDb != null)
                    {
                        credencialValida = true;
                        nombreCompleto = adminDb.NombreCompleto;
                    }
                }
                catch
                {
                }
            }

            if (!credencialValida)
            {
                return (false, "Credenciales incorrectas. Verifique el usuario (Gerente) y la contraseña.");
            }

            // Si el usuario actual era vendedor, guardarlo como usuarioVendedorPrevio para retornar a él
            if (string.Equals(_usuarioActual.Rol, "Vendedor", StringComparison.OrdinalIgnoreCase))
            {
                _usuarioVendedorPrevio = _usuarioActual;
            }

            _usuarioActual = adminDb ?? new Usuario
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
            CerrarSesionAdmin(out _);
        }

        public bool CerrarSesionAdmin(out bool requiereSeleccionarVendedor)
        {
            requiereSeleccionarVendedor = false;

            if (_usuarioVendedorPrevio != null && string.Equals(_usuarioVendedorPrevio.Rol, "Vendedor", StringComparison.OrdinalIgnoreCase))
            {
                _usuarioActual = _usuarioVendedorPrevio;
                OnUsuarioCambiado?.Invoke();
                return true;
            }

            // Si no hay vendedor previo (ej. se inició sesión como Gerente directamente),
            // se debe requerir seleccionar/ingresar la cuenta de vendedor deseada.
            requiereSeleccionarVendedor = true;
            return false;
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
