using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IInventoryService
    {
        List<Producto> ObtenerTodos();
        Task<List<Producto>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

        List<Producto> BuscarPorNombre(string termino);
        Task<List<Producto>> BuscarPorNombreAsync(string termino, CancellationToken cancellationToken = default);

        Producto? ObtenerPorId(int id);
        Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

        bool ValidarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError);
        Task<(bool EsValido, string MensajeError)> ValidarIngresoMercaderiaAsync(int productoId, decimal cantidadIngreso, CancellationToken cancellationToken = default);

        bool RegistrarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError);
        Task<(bool Exito, string MensajeError)> RegistrarIngresoMercaderiaAsync(int productoId, decimal cantidadIngreso, CancellationToken cancellationToken = default);

        bool AgregarOActualizarProducto(Producto producto);

        // CRUD y Gestión Masiva
        bool CrearProducto(Producto producto, out string error);
        Task<(bool Exito, string Error)> CrearProductoAsync(Producto producto, CancellationToken cancellationToken = default);

        bool ActualizarProducto(Producto producto, out string error);
        Task<(bool Exito, string Error)> ActualizarProductoAsync(Producto producto, CancellationToken cancellationToken = default);

        bool EliminarProducto(int id, out string error);
        Task<(bool Exito, string Error)> EliminarProductoAsync(int id, CancellationToken cancellationToken = default);

        List<string> ObtenerCategorias();
        Task<List<string>> ObtenerCategoriasAsync(CancellationToken cancellationToken = default);

        int ActualizarPreciosMasivamente(string? categoria, decimal porcentajeCambio, out string mensaje);
        Task<(int Modificados, string Mensaje)> ActualizarPreciosMasivamenteAsync(string? categoria, decimal porcentajeCambio, CancellationToken cancellationToken = default);
    }

    public class InventoryService : IInventoryService
    {
        public List<Producto> ObtenerTodos()
        {
            return ObtenerTodosAsync().GetAwaiter().GetResult();
        }

        public async Task<List<Producto>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Productos
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .ToListAsync(cancellationToken);
        }

        public List<Producto> BuscarPorNombre(string termino)
        {
            return BuscarPorNombreAsync(termino).GetAwaiter().GetResult();
        }

        public async Task<List<Producto>> BuscarPorNombreAsync(string termino, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            if (string.IsNullOrWhiteSpace(termino))
            {
                return await context.Productos
                    .AsNoTracking()
                    .OrderBy(p => p.Nombre)
                    .ToListAsync(cancellationToken);
            }

            var busqueda = termino.Trim().ToLower();
            return await context.Productos
                .AsNoTracking()
                .Where(p => p.Nombre.ToLower().Contains(busqueda) || 
                            p.Codigo.ToLower().Contains(busqueda) || 
                            p.Categoria.ToLower().Contains(busqueda))
                .OrderBy(p => p.Nombre)
                .ToListAsync(cancellationToken);
        }

        public Producto? ObtenerPorId(int id)
        {
            return ObtenerPorIdAsync(id).GetAwaiter().GetResult();
        }

        public async Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public bool ValidarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError)
        {
            var (esValido, error) = ValidarIngresoMercaderiaAsync(productoId, cantidadIngreso).GetAwaiter().GetResult();
            mensajeError = error;
            return esValido;
        }

        public async Task<(bool EsValido, string MensajeError)> ValidarIngresoMercaderiaAsync(int productoId, decimal cantidadIngreso, CancellationToken cancellationToken = default)
        {
            if (cantidadIngreso <= 0)
            {
                return (false, "La cantidad a ingresar debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            var producto = await context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken);

            if (producto == null)
            {
                return (false, "El producto no fue encontrado en la base de datos.");
            }

            // Regla INV-02 (CuMax): Si stock_actual + ingreso > CuMax -> "Límite físico de almacenamiento superado"
            if ((producto.StockActual + cantidadIngreso) > producto.CuMax)
            {
                return (false, "Límite físico de almacenamiento superado");
            }

            return (true, string.Empty);
        }

        public bool RegistrarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError)
        {
            var (exito, error) = RegistrarIngresoMercaderiaAsync(productoId, cantidadIngreso).GetAwaiter().GetResult();
            mensajeError = error;
            return exito;
        }

        public async Task<(bool Exito, string MensajeError)> RegistrarIngresoMercaderiaAsync(int productoId, decimal cantidadIngreso, CancellationToken cancellationToken = default)
        {
            var (esValido, valError) = await ValidarIngresoMercaderiaAsync(productoId, cantidadIngreso, cancellationToken);
            if (!esValido)
            {
                return (false, valError);
            }

            await using var context = new AppDbContext();
            var producto = await context.Productos.FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken);
            if (producto == null)
            {
                return (false, "El producto no fue encontrado.");
            }

            producto.StockActual += cantidadIngreso;
            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public bool AgregarOActualizarProducto(Producto producto)
        {
            using var context = new AppDbContext();
            if (producto.Id == 0)
            {
                context.Productos.Add(producto);
            }
            else
            {
                context.Productos.Update(producto);
            }
            context.SaveChanges();
            return true;
        }

        public bool CrearProducto(Producto producto, out string error)
        {
            var (exito, err) = CrearProductoAsync(producto).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> CrearProductoAsync(Producto producto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(producto.Codigo))
            {
                return (false, "El código del producto es obligatorio.");
            }
            if (string.IsNullOrWhiteSpace(producto.Nombre))
            {
                return (false, "El nombre del producto es obligatorio.");
            }
            if (producto.PrecioUnitario <= 0)
            {
                return (false, "El precio unitario debe ser mayor a cero.");
            }
            if (producto.CuMax < producto.StockActual)
            {
                return (false, "La capacidad máxima (CuMax) no puede ser menor al stock actual.");
            }

            await using var context = new AppDbContext();
            bool existeCodigo = await context.Productos
                .AsNoTracking()
                .AnyAsync(p => p.Codigo.ToLower() == producto.Codigo.Trim().ToLower(), cancellationToken);

            if (existeCodigo)
            {
                return (false, $"Ya existe un producto registrado con el código '{producto.Codigo}'.");
            }

            producto.Codigo = producto.Codigo.Trim().ToUpper();
            producto.Nombre = producto.Nombre.Trim();
            producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "General" : producto.Categoria.Trim();

            await context.Productos.AddAsync(producto, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public bool ActualizarProducto(Producto producto, out string error)
        {
            var (exito, err) = ActualizarProductoAsync(producto).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> ActualizarProductoAsync(Producto producto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(producto.Nombre))
            {
                return (false, "El nombre del producto es obligatorio.");
            }
            if (producto.PrecioUnitario <= 0)
            {
                return (false, "El precio unitario debe ser mayor a cero.");
            }
            if (producto.CuMax < producto.StockActual)
            {
                return (false, "La capacidad máxima (CuMax) no puede ser inferior al stock actual.");
            }

            await using var context = new AppDbContext();
            var existente = await context.Productos.FirstOrDefaultAsync(p => p.Id == producto.Id, cancellationToken);
            if (existente == null)
            {
                return (false, "Producto no encontrado en la base de datos.");
            }

            existente.Nombre = producto.Nombre.Trim();
            existente.Categoria = producto.Categoria.Trim();
            existente.PrecioUnitario = Math.Round(producto.PrecioUnitario, 2);
            existente.StockActual = producto.StockActual;
            existente.StockMinimo = producto.StockMinimo;
            existente.CuMax = producto.CuMax;
            existente.UnidadMedida = producto.UnidadMedida;

            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public bool EliminarProducto(int id, out string error)
        {
            var (exito, err) = EliminarProductoAsync(id).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> EliminarProductoAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            var producto = await context.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (producto == null)
            {
                return (false, "Producto no encontrado.");
            }

            // Validar si tiene ventas asociadas
            bool tieneVentas = await context.DetallesVenta
                .AsNoTracking()
                .AnyAsync(d => d.ProductoId == id, cancellationToken);

            if (tieneVentas)
            {
                return (false, "No se puede eliminar el producto porque tiene ventas históricas asociadas. Se recomienda ajustar su stock a cero.");
            }

            context.Productos.Remove(producto);
            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public List<string> ObtenerCategorias()
        {
            return ObtenerCategoriasAsync().GetAwaiter().GetResult();
        }

        public async Task<List<string>> ObtenerCategoriasAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Productos
                .AsNoTracking()
                .Select(p => p.Categoria)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken);
        }

        public int ActualizarPreciosMasivamente(string? categoria, decimal porcentajeCambio, out string mensaje)
        {
            var (modificados, msg) = ActualizarPreciosMasivamenteAsync(categoria, porcentajeCambio).GetAwaiter().GetResult();
            mensaje = msg;
            return modificados;
        }

        public async Task<(int Modificados, string Mensaje)> ActualizarPreciosMasivamenteAsync(string? categoria, decimal porcentajeCambio, CancellationToken cancellationToken = default)
        {
            if (porcentajeCambio == 0)
            {
                return (0, "El porcentaje de cambio debe ser diferente de 0%.");
            }

            await using var context = new AppDbContext();
            var query = context.Productos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(categoria) && categoria != "Todas")
            {
                query = query.Where(p => p.Categoria == categoria);
            }

            var lista = await query.ToListAsync(cancellationToken);
            if (!lista.Any())
            {
                return (0, "No se encontraron productos para actualizar.");
            }

            decimal factor = 1m + (porcentajeCambio / 100m);
            foreach (var prod in lista)
            {
                decimal nuevoPrecio = Math.Round(prod.PrecioUnitario * factor, 2);
                prod.PrecioUnitario = Math.Max(0.10m, nuevoPrecio);
            }

            await context.SaveChangesAsync(cancellationToken);
            string accion = porcentajeCambio > 0 ? "incrementaron" : "redujeron";
            string msg = $"Se {accion} los precios de {lista.Count} producto(s) en un {Math.Abs(porcentajeCambio)}%.";
            return (lista.Count, msg);
        }
    }
}
