using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface ISalesService
    {
        bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError);
        decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario);
        bool ProcesarVenta(Venta venta, out string mensajeError);
        List<Venta> ObtenerVentasRecientes(int limite = 20);
    }

    public class SalesService : ISalesService
    {
        public bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError)
        {
            mensajeError = string.Empty;

            if (cantidadSolicitada <= 0)
            {
                mensajeError = "La cantidad solicitada debe ser mayor a cero.";
                return false;
            }

            using var context = new AppDbContext();
            var producto = context.Productos.FirstOrDefault(p => p.Id == productoId);
            if (producto == null)
            {
                mensajeError = "Producto no encontrado.";
                return false;
            }

            // Regla INV-01 (Bloqueo de Venta):
            // Si cantidad_solicitada > stock_actual, el PosViewModel debe bloquear la acción y lanzar un MessageBox
            // con el texto: "Stock insuficiente".
            if (cantidadSolicitada > producto.StockActual)
            {
                mensajeError = "Stock insuficiente";
                return false;
            }

            return true;
        }

        public decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario)
        {
            // Regla POS-02 (Fracciones):
            // Permitir ingreso de cantidades en decimales (double o decimal),
            // multiplicando matemáticamente por el precio unitario.
            return Math.Round(cantidad * precioUnitario, 2);
        }

        public bool ProcesarVenta(Venta venta, out string mensajeError)
        {
            mensajeError = string.Empty;

            if (venta.Detalles == null || !venta.Detalles.Any())
            {
                mensajeError = "El ticket no contiene productos.";
                return false;
            }

            using var context = new AppDbContext();
            using var transaction = context.Database.BeginTransaction();

            try
            {
                foreach (var item in venta.Detalles)
                {
                    var producto = context.Productos.FirstOrDefault(p => p.Id == item.ProductoId);
                    if (producto == null)
                    {
                        mensajeError = $"El producto ID {item.ProductoId} no existe.";
                        transaction.Rollback();
                        return false;
                    }

                    // Regla INV-01: verificación atómica antes de confirmar la venta
                    if (item.Cantidad > producto.StockActual)
                    {
                        mensajeError = "Stock insuficiente";
                        transaction.Rollback();
                        return false;
                    }

                    // Descontar stock matemático
                    producto.StockActual -= item.Cantidad;
                }

                venta.Fecha = DateTime.Now;
                venta.Total = Math.Round(venta.Detalles.Sum(d => d.Subtotal), 2);
                if (string.IsNullOrWhiteSpace(venta.NumeroTicket))
                {
                    var count = context.Ventas.Count() + 1;
                    venta.NumeroTicket = $"TCK-{DateTime.Now:yyyyMMdd}-{count:D4}";
                }

                context.Ventas.Add(venta);
                context.SaveChanges();
                transaction.Commit();

                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                mensajeError = $"Error al registrar la venta: {ex.Message}";
                return false;
            }
        }

        public List<Venta> ObtenerVentasRecientes(int limite = 20)
        {
            using var context = new AppDbContext();
            return context.Ventas
                .Include(v => v.Detalles)
                .OrderByDescending(v => v.Fecha)
                .Take(limite)
                .ToList();
        }
    }
}
