using System;
using System.Collections.Generic;
using System.Linq;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IInventoryService
    {
        List<Producto> ObtenerTodos();
        List<Producto> BuscarPorNombre(string termino);
        Producto? ObtenerPorId(int id);
        bool ValidarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError);
        bool RegistrarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError);
        bool AgregarOActualizarProducto(Producto producto);

        // CRUD y Gestión Masiva
        bool CrearProducto(Producto producto, out string error);
        bool ActualizarProducto(Producto producto, out string error);
        bool EliminarProducto(int id, out string error);
        List<string> ObtenerCategorias();
        int ActualizarPreciosMasivamente(string? categoria, decimal porcentajeCambio, out string mensaje);
    }

    public class InventoryService : IInventoryService
    {
        public List<Producto> ObtenerTodos()
        {
            using var context = new AppDbContext();
            return context.Productos.OrderBy(p => p.Nombre).ToList();
        }

        public List<Producto> BuscarPorNombre(string termino)
        {
            // Regla POS-01 (Búsqueda): La búsqueda de productos en el POS es manual por nombre (no se asume lector de código de barras).
            using var context = new AppDbContext();
            if (string.IsNullOrWhiteSpace(termino))
            {
                return context.Productos.OrderBy(p => p.Nombre).ToList();
            }

            var busqueda = termino.Trim().ToLower();
            return context.Productos
                .Where(p => p.Nombre.ToLower().Contains(busqueda) || p.Codigo.ToLower().Contains(busqueda) || p.Categoria.ToLower().Contains(busqueda))
                .OrderBy(p => p.Nombre)
                .ToList();
        }

        public Producto? ObtenerPorId(int id)
        {
            using var context = new AppDbContext();
            return context.Productos.FirstOrDefault(p => p.Id == id);
        }

        public bool ValidarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError)
        {
            mensajeError = string.Empty;

            if (cantidadIngreso <= 0)
            {
                mensajeError = "La cantidad a ingresar debe ser mayor a cero.";
                return false;
            }

            using var context = new AppDbContext();
            var producto = context.Productos.FirstOrDefault(p => p.Id == productoId);
            if (producto == null)
            {
                mensajeError = "El producto no fue encontrado en la base de datos.";
                return false;
            }

            // Regla INV-02 (CuMax):
            // Al ingresar nueva mercadería al almacén, si stock_actual + ingreso > CuMax, lanzar un MessageBox:
            // "Límite físico de almacenamiento superado"
            if ((producto.StockActual + cantidadIngreso) > producto.CuMax)
            {
                mensajeError = "Límite físico de almacenamiento superado";
                return false;
            }

            return true;
        }

        public bool RegistrarIngresoMercaderia(int productoId, decimal cantidadIngreso, out string mensajeError)
        {
            if (!ValidarIngresoMercaderia(productoId, cantidadIngreso, out mensajeError))
            {
                return false;
            }

            using var context = new AppDbContext();
            var producto = context.Productos.FirstOrDefault(p => p.Id == productoId);
            if (producto == null)
            {
                mensajeError = "El producto no fue encontrado.";
                return false;
            }

            producto.StockActual += cantidadIngreso;
            context.SaveChanges();
            return true;
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
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(producto.Codigo))
            {
                error = "El código del producto es obligatorio.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(producto.Nombre))
            {
                error = "El nombre del producto es obligatorio.";
                return false;
            }
            if (producto.PrecioUnitario <= 0)
            {
                error = "El precio unitario debe ser mayor a cero.";
                return false;
            }
            if (producto.CuMax < producto.StockActual)
            {
                error = "La capacidad máxima (CuMax) no puede ser menor al stock actual.";
                return false;
            }

            using var context = new AppDbContext();
            if (context.Productos.Any(p => p.Codigo.ToLower() == producto.Codigo.Trim().ToLower()))
            {
                error = $"Ya existe un producto registrado con el código '{producto.Codigo}'.";
                return false;
            }

            producto.Codigo = producto.Codigo.Trim().ToUpper();
            producto.Nombre = producto.Nombre.Trim();
            producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "General" : producto.Categoria.Trim();

            context.Productos.Add(producto);
            context.SaveChanges();
            return true;
        }

        public bool ActualizarProducto(Producto producto, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(producto.Nombre))
            {
                error = "El nombre del producto es obligatorio.";
                return false;
            }
            if (producto.PrecioUnitario <= 0)
            {
                error = "El precio unitario debe ser mayor a cero.";
                return false;
            }
            if (producto.CuMax < producto.StockActual)
            {
                error = "La capacidad máxima (CuMax) no puede ser inferior al stock actual.";
                return false;
            }

            using var context = new AppDbContext();
            var existente = context.Productos.FirstOrDefault(p => p.Id == producto.Id);
            if (existente == null)
            {
                error = "Producto no encontrado en la base de datos.";
                return false;
            }

            existente.Nombre = producto.Nombre.Trim();
            existente.Categoria = producto.Categoria.Trim();
            existente.PrecioUnitario = Math.Round(producto.PrecioUnitario, 2);
            existente.StockActual = producto.StockActual;
            existente.StockMinimo = producto.StockMinimo;
            existente.CuMax = producto.CuMax;
            existente.UnidadMedida = producto.UnidadMedida;

            context.SaveChanges();
            return true;
        }

        public bool EliminarProducto(int id, out string error)
        {
            error = string.Empty;

            using var context = new AppDbContext();
            var producto = context.Productos.FirstOrDefault(p => p.Id == id);
            if (producto == null)
            {
                error = "Producto no encontrado.";
                return false;
            }

            // Validar si tiene ventas asociadas
            if (context.DetallesVenta.Any(d => d.ProductoId == id))
            {
                error = "No se puede eliminar el producto porque tiene ventas históricas asociadas. Se recomienda ajustar su stock a cero.";
                return false;
            }

            context.Productos.Remove(producto);
            context.SaveChanges();
            return true;
        }

        public List<string> ObtenerCategorias()
        {
            using var context = new AppDbContext();
            return context.Productos
                .Select(p => p.Categoria)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public int ActualizarPreciosMasivamente(string? categoria, decimal porcentajeCambio, out string mensaje)
        {
            mensaje = string.Empty;

            if (porcentajeCambio == 0)
            {
                mensaje = "El porcentaje de cambio debe ser diferente de 0%.";
                return 0;
            }

            using var context = new AppDbContext();
            var query = context.Productos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(categoria) && categoria != "Todas")
            {
                query = query.Where(p => p.Categoria == categoria);
            }

            var lista = query.ToList();
            if (!lista.Any())
            {
                mensaje = "No se encontraron productos para actualizar.";
                return 0;
            }

            decimal factor = 1m + (porcentajeCambio / 100m);
            foreach (var prod in lista)
            {
                decimal nuevoPrecio = Math.Round(prod.PrecioUnitario * factor, 2);
                prod.PrecioUnitario = Math.Max(0.10m, nuevoPrecio);
            }

            context.SaveChanges();
            string accion = porcentajeCambio > 0 ? "incrementaron" : "redujeron";
            mensaje = $"Se {accion} los precios de {lista.Count} producto(s) en un {Math.Abs(porcentajeCambio)}%.";
            return lista.Count;
        }
    }
}
