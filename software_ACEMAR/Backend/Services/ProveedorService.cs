using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IProveedorService
    {
        List<Proveedor> ObtenerTodos();
        List<Proveedor> Buscar(string texto);
        List<Proveedor> ObtenerProveedoresDeProducto(int productoId);
        bool Crear(Proveedor proveedor, out string error);
        bool Actualizar(Proveedor proveedor, out string error);
        bool Eliminar(int id, out string error);

        /// <summary>Asocia un proveedor a un producto. Si esHabitual=true, desmarca el previo habitual.</summary>
        bool AsociarAProducto(int proveedorId, int productoId, bool esHabitual, out string error);

        /// <summary>Desasocia un proveedor de un producto.</summary>
        bool DesasociarDeProducto(int proveedorId, int productoId, out string error);
    }

    public class ProveedorService : IProveedorService
    {
        public List<Proveedor> ObtenerTodos()
        {
            using var ctx = new AppDbContext();
            return ctx.Proveedores
                      .Include(p => p.ProductosProveedor)
                          .ThenInclude(pp => pp.Producto)
                      .OrderBy(p => p.Nombre)
                      .ToList();
        }

        public List<Proveedor> Buscar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return ObtenerTodos();

            using var ctx = new AppDbContext();
            texto = texto.ToLower().Trim();
            return ctx.Proveedores
                      .Include(p => p.ProductosProveedor)
                          .ThenInclude(pp => pp.Producto)
                      .Where(p => p.Nombre.ToLower().Contains(texto)
                               || (p.Telefono != null && p.Telefono.Contains(texto))
                               || (p.Contacto != null && p.Contacto.ToLower().Contains(texto)))
                      .OrderBy(p => p.Nombre)
                      .ToList();
        }

        public List<Proveedor> ObtenerProveedoresDeProducto(int productoId)
        {
            using var ctx = new AppDbContext();
            return ctx.ProductosProveedores
                      .Include(pp => pp.Proveedor)
                      .Where(pp => pp.ProductoId == productoId)
                      .OrderByDescending(pp => pp.EsHabitual)
                      .Select(pp => pp.Proveedor!)
                      .ToList();
        }

        public bool Crear(Proveedor proveedor, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                error = "El nombre del proveedor es obligatorio.";
                return false;
            }

            using var ctx = new AppDbContext();
            proveedor.FechaRegistro = DateTime.Now;
            proveedor.Nombre = proveedor.Nombre.Trim();
            ctx.Proveedores.Add(proveedor);
            ctx.SaveChanges();
            return true;
        }

        public bool Actualizar(Proveedor proveedor, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                error = "El nombre del proveedor es obligatorio.";
                return false;
            }

            using var ctx = new AppDbContext();
            var existente = ctx.Proveedores.Find(proveedor.Id);
            if (existente == null)
            {
                error = "Proveedor no encontrado.";
                return false;
            }

            existente.Nombre       = proveedor.Nombre.Trim();
            existente.Contacto     = proveedor.Contacto?.Trim();
            existente.Telefono     = proveedor.Telefono?.Trim();
            existente.WhatsApp     = proveedor.WhatsApp?.Trim();
            existente.Correo       = proveedor.Correo?.Trim();
            existente.Direccion    = proveedor.Direccion?.Trim();
            existente.Observaciones = proveedor.Observaciones?.Trim();

            ctx.SaveChanges();
            return true;
        }

        public bool Eliminar(int id, out string error)
        {
            error = string.Empty;
            using var ctx = new AppDbContext();
            var proveedor = ctx.Proveedores
                              .Include(p => p.ProductosProveedor)
                              .FirstOrDefault(p => p.Id == id);

            if (proveedor == null)
            {
                error = "Proveedor no encontrado.";
                return false;
            }

            // Eliminar asociaciones primero
            ctx.ProductosProveedores.RemoveRange(proveedor.ProductosProveedor);
            ctx.Proveedores.Remove(proveedor);
            ctx.SaveChanges();
            return true;
        }

        public bool AsociarAProducto(int proveedorId, int productoId, bool esHabitual, out string error)
        {
            error = string.Empty;
            using var ctx = new AppDbContext();

            // Verificar que no estén ya asociados
            var existente = ctx.ProductosProveedores
                              .FirstOrDefault(pp => pp.ProveedorId == proveedorId && pp.ProductoId == productoId);
            if (existente != null)
            {
                existente.EsHabitual = esHabitual;
            }
            else
            {
                ctx.ProductosProveedores.Add(new ProductoProveedor
                {
                    ProductoId  = productoId,
                    ProveedorId = proveedorId,
                    EsHabitual  = esHabitual
                });
            }

            // Si se marca como habitual, desmarcar el previo
            if (esHabitual)
            {
                var previosHabituales = ctx.ProductosProveedores
                    .Where(pp => pp.ProductoId == productoId
                              && pp.ProveedorId != proveedorId
                              && pp.EsHabitual)
                    .ToList();
                foreach (var pp in previosHabituales)
                    pp.EsHabitual = false;
            }

            ctx.SaveChanges();
            return true;
        }

        public bool DesasociarDeProducto(int proveedorId, int productoId, out string error)
        {
            error = string.Empty;
            using var ctx = new AppDbContext();
            var rel = ctx.ProductosProveedores
                         .FirstOrDefault(pp => pp.ProveedorId == proveedorId && pp.ProductoId == productoId);
            if (rel == null)
            {
                error = "La asociación no existe.";
                return false;
            }

            ctx.ProductosProveedores.Remove(rel);
            ctx.SaveChanges();
            return true;
        }
    }
}
