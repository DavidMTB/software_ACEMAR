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
    public interface ISalesService
    {
        bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError);
        Task<(bool EsValido, string MensajeError)> ValidarStockDisponibleAsync(int productoId, decimal cantidadSolicitada, CancellationToken cancellationToken = default);
        
        decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario);
        
        bool ProcesarVenta(Venta venta, out string mensajeError);
        Task<(bool Exito, string MensajeError)> ProcesarVentaAsync(Venta venta, CancellationToken cancellationToken = default);
        
        List<Venta> ObtenerVentasRecientes(int limite = 20);
        Task<List<Venta>> ObtenerVentasRecientesAsync(int limite = 20, CancellationToken cancellationToken = default);
    }

    public class SalesService : ISalesService
    {
        public bool ValidarStockDisponible(int productoId, decimal cantidadSolicitada, out string mensajeError)
        {
            var (esValido, error) = ValidarStockDisponibleAsync(productoId, cantidadSolicitada).GetAwaiter().GetResult();
            mensajeError = error;
            return esValido;
        }

        public async Task<(bool EsValido, string MensajeError)> ValidarStockDisponibleAsync(int productoId, decimal cantidadSolicitada, CancellationToken cancellationToken = default)
        {
            if (cantidadSolicitada <= 0)
            {
                return (false, "La cantidad solicitada debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            var producto = await context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken);

            if (producto == null)
            {
                return (false, "Producto no encontrado.");
            }

            // Regla INV-01 (Bloqueo de Venta): Si cantidad_solicitada > stock_actual -> "Stock insuficiente"
            if (cantidadSolicitada > producto.StockActual)
            {
                return (false, "Stock insuficiente");
            }

            return (true, string.Empty);
        }

        public decimal MultiplicarPrecioFraccion(decimal cantidad, decimal precioUnitario)
        {
            // Regla POS-02 (Fracciones): Multiplicar matemáticamente por precio unitario redondeado a 2 decimales
            return Math.Round(cantidad * precioUnitario, 2);
        }

        public bool ProcesarVenta(Venta venta, out string mensajeError)
        {
            var (exito, error) = ProcesarVentaAsync(venta).GetAwaiter().GetResult();
            mensajeError = error;
            return exito;
        }

        public async Task<(bool Exito, string MensajeError)> ProcesarVentaAsync(Venta venta, CancellationToken cancellationToken = default)
        {
            if (!venta.EsValida(out string valError))
            {
                return (false, valError);
            }

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Optimización N+1: Cargar todos los productos involucrados en una sola consulta
                var productoIds = venta.Detalles.Select(d => d.ProductoId).Distinct().ToList();
                var productosDict = await context.Productos
                    .Where(p => productoIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                foreach (var item in venta.Detalles)
                {
                    // Evitar colisión de tracking en EF Core anulando la propiedad de navegación
                    item.Producto = null;

                    if (!productosDict.TryGetValue(item.ProductoId, out var producto))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return (false, $"El producto ID {item.ProductoId} no existe.");
                    }

                    // Regla INV-01: Verificación atómica antes de confirmar la venta
                    if (item.Cantidad > producto.StockActual)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return (false, "Stock insuficiente");
                    }

                    // Descontar stock atómico
                    producto.StockActual -= item.Cantidad;
                }

                venta.ClienteCredito = null;

                venta.Fecha = DateTime.Now;
                venta.RecalcularTotal();

                if (string.IsNullOrWhiteSpace(venta.NumeroTicket))
                {
                    // Optimización de secuencia sin Count() masivo
                    var maxId = await context.Ventas.MaxAsync(v => (int?)v.Id, cancellationToken) ?? 0;
                    venta.NumeroTicket = $"TCK-{DateTime.Now:yyyyMMdd}-{(maxId + 1):D4}";
                }

                await context.Ventas.AddAsync(venta, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Error al registrar la venta: {ex.Message}");
            }
        }

        public List<Venta> ObtenerVentasRecientes(int limite = 20)
        {
            return ObtenerVentasRecientesAsync(limite).GetAwaiter().GetResult();
        }

        public async Task<List<Venta>> ObtenerVentasRecientesAsync(int limite = 20, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                .Include(v => v.ClienteCredito)
                .OrderByDescending(v => v.Fecha)
                .Take(limite)
                .ToListAsync(cancellationToken);
        }
    }
}
