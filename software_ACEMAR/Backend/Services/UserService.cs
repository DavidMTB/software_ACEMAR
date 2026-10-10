using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IUserService
    {
        Task<List<Usuario>> ObtenerTodosUsuariosAsync();
        Task<(bool Exito, string Error, Usuario? UsuarioCreado)> RegistrarVendedorAsync(string nombreUsuario, string password, string mostradorAsignado = "Mostrador 01");
        Task<(bool Exito, string Error)> ActualizarUsuarioAsync(int id, string nuevoNombreUsuario, string nuevaPassword, string mostradorAsignado = "Mostrador 01");
        Task<(bool Exito, string Error)> EliminarUsuarioAsync(int id);
        Task<Usuario?> ValidarCredencialesAsync(string usuario, string password);
    }

    public class UserService : IUserService
    {
        public async Task<List<Usuario>> ObtenerTodosUsuariosAsync()
        {
            await using var context = new AppDbContext();
            return await context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.Rol == "Administrador" ? 0 : 1)
                .ThenBy(u => u.Id)
                .ToListAsync();
        }

        public async Task<(bool Exito, string Error, Usuario? UsuarioCreado)> RegistrarVendedorAsync(string nombreUsuario, string password, string mostradorAsignado = "Mostrador 01")
        {
            if (string.IsNullOrWhiteSpace(nombreUsuario))
            {
                return (false, "El nombre de usuario es obligatorio.", null);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "La contraseña es obligatoria.", null);
            }

            string usernameTrim = nombreUsuario.Trim();

            await using var context = new AppDbContext();
            var existe = await context.Usuarios.AnyAsync(u => u.NombreUsuario.ToLower() == usernameTrim.ToLower());
            if (existe)
            {
                return (false, $"El nombre de usuario '{usernameTrim}' ya existe. Elija otro nombre.", null);
            }

            var nuevoUsuario = new Usuario
            {
                NombreUsuario = usernameTrim,
                NombreCompleto = usernameTrim,
                Password = password.Trim(),
                Rol = "Vendedor",
                MostradorAsignado = string.IsNullOrWhiteSpace(mostradorAsignado) ? "Mostrador 01" : mostradorAsignado.Trim()
            };

            context.Usuarios.Add(nuevoUsuario);
            await context.SaveChangesAsync();

            return (true, string.Empty, nuevoUsuario);
        }

        public async Task<(bool Exito, string Error)> ActualizarUsuarioAsync(int id, string nuevoNombreUsuario, string nuevaPassword, string mostradorAsignado = "Mostrador 01")
        {
            if (string.IsNullOrWhiteSpace(nuevoNombreUsuario))
            {
                return (false, "El nombre de usuario no puede estar vacío.");
            }

            string usernameTrim = nuevoNombreUsuario.Trim();

            await using var context = new AppDbContext();
            var usuarioDb = await context.Usuarios.FindAsync(id);
            if (usuarioDb == null)
            {
                return (false, "El usuario especificado no existe.");
            }

            // Verificar duplicados de nombre (excepto él mismo)
            var existeOtro = await context.Usuarios.AnyAsync(u => u.Id != id && u.NombreUsuario.ToLower() == usernameTrim.ToLower());
            if (existeOtro)
            {
                return (false, $"El nombre de usuario '{usernameTrim}' ya pertenece a otro usuario.");
            }

            usuarioDb.NombreUsuario = usernameTrim;
            usuarioDb.NombreCompleto = usernameTrim;
            usuarioDb.MostradorAsignado = string.IsNullOrWhiteSpace(mostradorAsignado) ? "Mostrador 01" : mostradorAsignado.Trim();

            if (!string.IsNullOrWhiteSpace(nuevaPassword))
            {
                usuarioDb.Password = nuevaPassword.Trim();
            }

            await context.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool Exito, string Error)> EliminarUsuarioAsync(int id)
        {
            await using var context = new AppDbContext();
            var usuarioDb = await context.Usuarios.FindAsync(id);
            if (usuarioDb == null)
            {
                return (false, "El usuario especificado no existe.");
            }

            if (usuarioDb.Rol == "Administrador" || string.Equals(usuarioDb.NombreUsuario, "Gerente", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "No se puede eliminar la cuenta principal de Administrador por seguridad del sistema.");
            }

            context.Usuarios.Remove(usuarioDb);
            await context.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task<Usuario?> ValidarCredencialesAsync(string usuario, string password)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            string userLower = usuario.Trim().ToLower();
            string passTrim = password.Trim();

            await using var context = new AppDbContext();
            var userDb = await context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.NombreUsuario.ToLower() == userLower && u.Password == passTrim);

            return userDb;
        }
    }
}
