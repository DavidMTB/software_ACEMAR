using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IVentaCorrectionService
    {
        Venta? ObtenerVenta(int ventaId);
        List<Venta> ObtenerVentasCorregibles(DateTime? fecha = null);

        Task<(bool Exito, string Error)> CorregirVentaAsync(
            int ventaId,
            List<DetalleVenta> nuevosDetalles,
            string? nuevoClienteNombre,
            string usuarioModifica);

        (bool Exito, string Error) CorregirDetalleVenta(int ventaId, int detalleId, decimal nuevaCantidad, decimal nuevoPrecio, string motivo, string usuario);

        Task<(bool Exito, string Error)> AnularVentaAsync(int ventaId, string motivo, string usuarioModifica);

        (bool Exito, string Error) AnularVentaCompleta(int ventaId, string motivo, string usuario);

        List<VentaAuditoria> ObtenerAuditoria(int ventaId);
        List<VentaAuditoria> ObtenerHistorialAuditoria(int ventaId);
    }

    public class VentaCorrectionService : IVentaCorrectionService
    {
        private const int LIMITE_DIAS_CORRECCION = 30;
        private readonly ILoteService _loteService;

        public VentaCorrectionService() : this(new LoteService()) { }

        public VentaCorrectionService(ILoteService loteService)
        {
            _loteService = loteService;
        }

        public (bool Exito, string Error) CorregirDetalleVenta(int ventaId, int detalleId, decimal nuevaCantidad, decimal nuevoPrecio, string motivo, string usuario)
        {
            var venta = ObtenerVenta(ventaId);
            if (venta == null) return (false, "Venta no encontrada.");

            var detallesClonados = venta.Detalles.Select(d => new DetalleVenta
            {
                Id = d.Id,
                VentaId = d.VentaId,
                ProductoId = d.ProductoId,
                LoteProductoId = d.LoteProductoId,
                ProductoNombre = d.ProductoNombre,
                UnidadMedida = d.UnidadMedida,
                Cantidad = d.Id == detalleId ? nuevaCantidad : d.Cantidad,
                PrecioUnitario = d.Id == detalleId ? nuevoPrecio : d.PrecioUnitario
            }).Where(d => d.Cantidad > 0).ToList();

            return CorregirVentaAsync(ventaId, detallesClonados, venta.ClienteNombre, usuario).GetAwaiter().GetResult();
        }

        public (bool Exito, string Error) AnularVentaCompleta(int ventaId, string motivo, string usuario)
        {
            return AnularVentaAsync(ventaId, motivo, usuario).GetAwaiter().GetResult();
        }

        public List<VentaAuditoria> ObtenerHistorialAuditoria(int ventaId)
        {
            return ObtenerAuditoria(ventaId);
        }

        public Venta? ObtenerVenta(int ventaId)
        {
            using var ctx = new AppDbContext();
            return ctx.Ventas
                      .Include(v => v.Detalles)
                      .FirstOrDefault(v => v.Id == ventaId);
        }

        public List<Venta> ObtenerVentasCorregibles(DateTime? fecha = null)
        {
            using var ctx = new AppDbContext();
            var limite = DateTime.Now.AddDays(-LIMITE_DIAS_CORRECCION);
            var query  = ctx.Ventas
                            .Include(v => v.Detalles)
                            .Where(v => v.Fecha >= limite);

            if (fecha.HasValue)
            {
                var inicio = fecha.Value.Date;
                var fin    = inicio.AddDays(1);
                query = query.Where(v => v.Fecha >= inicio && v.Fecha < fin);
            }

            return query.OrderByDescending(v => v.Fecha).ToList();
        }

        public async Task<(bool Exito, string Error)> CorregirVentaAsync(
            int ventaId,
            List<DetalleVenta> nuevosDetalles,
            string? nuevoClienteNombre,
            string usuarioModifica)
        {
            using var ctx = new AppDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();

            try
            {
                var venta = await ctx.Ventas
                                     .Include(v => v.Detalles)
                                     .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                    return (false, "Venta no encontrada.");

                if ((DateTime.Now - venta.Fecha).TotalDays > LIMITE_DIAS_CORRECCION)
                    return (false, $"Solo se pueden corregir ventas procesadas en los últimos {LIMITE_DIAS_CORRECCION} días.");

                // Guardar foto previa para auditoría en JSON
                string datosPreviosJson = JsonSerializer.Serialize(new
                {
                    venta.Total,
                    venta.ClienteNombre,
                    Detalles = venta.Detalles.Select(d => new
                    {
                        d.ProductoId,
                        d.ProductoNombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal,
                        d.LoteProductoId
                    })
                });

                // 1. Revertir stock de lotes anteriores de forma atómica dentro del mismo DbContext
                foreach (var d in venta.Detalles)
                {
                    var prod = await ctx.Productos.FindAsync(d.ProductoId);
                    decimal factorRev = prod != null ? prod.ObtenerFactorParaUnidad(d.UnidadMedida) : 1.0m;
                    decimal cantBaseRev = d.Cantidad * factorRev;

                    if (d.LoteProductoId.HasValue)
                    {
                        var lote = await ctx.LotesProducto.FindAsync(d.LoteProductoId.Value);
                        if (lote != null)
                        {
                            lote.CantidadDisponible += cantBaseRev;
                            lote.Estado = "Activo";
                        }
                    }
                    if (prod != null)
                    {
                        prod.StockActual += cantBaseRev;
                    }
                }

                // 2. Eliminar detalles viejos
                ctx.DetallesVenta.RemoveRange(venta.Detalles);
                venta.Detalles.Clear();

                // 3. Consumir stock para nuevos detalles atómicamente
                decimal nuevoTotal = 0;
                foreach (var nd in nuevosDetalles)
                {
                    var prod = await ctx.Productos.FindAsync(nd.ProductoId);
                    if (prod == null) continue;

                    string um = nd.UnidadMedida ?? prod.UnidadMedida;
                    decimal factorCons = prod.ObtenerFactorParaUnidad(um);
                    decimal cantBaseCons = nd.Cantidad * factorCons;

                    int? loteAsignadoId = null;
                    var lote = await ctx.LotesProducto
                        .Where(l => l.ProductoId == nd.ProductoId && l.Estado == "Activo" && l.CantidadDisponible > 0)
                        .OrderBy(l => l.FechaIngreso)
                        .FirstOrDefaultAsync();

                    if (lote != null)
                    {
                        if (lote.CantidadDisponible < cantBaseCons)
                        {
                            await tx.RollbackAsync();
                            return (false, $"Stock insuficiente en lote activo para '{nd.ProductoNombre}'. Disponible: {lote.CantidadDisponible:0.##}, solicitado: {cantBaseCons:0.##} {prod.UnidadMedida}.");
                        }
                        lote.CantidadDisponible -= cantBaseCons;
                        if (lote.CantidadDisponible <= 0)
                        {
                            lote.CantidadDisponible = 0;
                            lote.Estado = "Agotado";
                        }
                        prod.StockActual -= cantBaseCons;
                        loteAsignadoId = lote.Id;
                    }
                    else
                    {
                        if (prod.StockActual < cantBaseCons)
                        {
                            await tx.RollbackAsync();
                            return (false, $"Stock insuficiente para '{nd.ProductoNombre}'.");
                        }
                        prod.StockActual -= cantBaseCons;
                    }

                    var detalleNuevo = new DetalleVenta
                    {
                        VentaId        = ventaId,
                        ProductoId     = nd.ProductoId,
                        ProductoNombre = nd.ProductoNombre,
                        Cantidad       = nd.Cantidad,
                        PrecioUnitario = nd.PrecioUnitario > 0 ? nd.PrecioUnitario : prod.PrecioUnitario,
                        LoteProductoId = loteAsignadoId,
                        UnidadMedida   = nd.UnidadMedida ?? prod.UnidadMedida
                    };
                    nuevoTotal += detalleNuevo.Subtotal;
                    venta.Detalles.Add(detalleNuevo);
                }

                // 4. Si fue venta a crédito, ajustar la diferencia de deuda
                if (venta.TipoVenta == TipoVentaConsts.Credito && venta.ClienteCreditoId.HasValue)
                {
                    var cliente = await ctx.ClientesCredito.FindAsync(venta.ClienteCreditoId.Value);
                    if (cliente != null)
                    {
                        decimal diferencia = nuevoTotal - venta.Total;
                        cliente.DeudaActual += diferencia;
                        if (cliente.DeudaActual < 0) cliente.DeudaActual = 0;
                    }
                }

                venta.Total             = nuevoTotal;
                if (!string.IsNullOrWhiteSpace(nuevoClienteNombre))
                    venta.ClienteNombre = nuevoClienteNombre.Trim();

                venta.FueModificada     = true;
                venta.FechaModificacion = DateTime.Now;

                string datosNuevosJson = JsonSerializer.Serialize(new
                {
                    venta.Total,
                    venta.ClienteNombre,
                    Detalles = venta.Detalles.Select(d => new
                    {
                        d.ProductoId,
                        d.ProductoNombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal,
                        d.LoteProductoId
                    })
                });

                // Registrar auditoría
                var auditoria = new VentaAuditoria
                {
                    VentaId           = ventaId,
                    FechaModificacion = DateTime.Now,
                    UsuarioModifica   = usuarioModifica,
                    Descripcion       = "Modificación de Detalle: Corrección de cantidades/precios emitida por Administrador",
                    ValorAnterior     = datosPreviosJson,
                    ValorNuevo        = datosNuevosJson
                };
                await ctx.VentasAuditoria.AddAsync(auditoria);

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Error al corregir venta: {ex.Message}");
            }
        }

        public async Task<(bool Exito, string Error)> AnularVentaAsync(
            int ventaId, string motivo, string usuarioModifica)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                return (false, "El motivo de anulación es obligatorio.");

            using var ctx = new AppDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();

            try
            {
                var venta = await ctx.Ventas
                                     .Include(v => v.Detalles)
                                     .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                    return (false, "Venta no encontrada.");

                string datosPreviosJson = JsonSerializer.Serialize(new
                {
                    venta.Total,
                    venta.ClienteNombre,
                    Detalles = venta.Detalles.Select(d => new
                    {
                        d.ProductoId,
                        d.ProductoNombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal
                    })
                });

                // Revertir stock de lotes atómicamente dentro del mismo DbContext
                foreach (var d in venta.Detalles)
                {
                    var prod = await ctx.Productos.FindAsync(d.ProductoId);
                    decimal factorRev = prod != null ? prod.ObtenerFactorParaUnidad(d.UnidadMedida) : 1.0m;
                    decimal cantBaseRev = d.Cantidad * factorRev;

                    if (d.LoteProductoId.HasValue)
                    {
                        var lote = await ctx.LotesProducto.FindAsync(d.LoteProductoId.Value);
                        if (lote != null)
                        {
                            lote.CantidadDisponible += cantBaseRev;
                            lote.Estado = "Activo";
                        }
                    }
                    if (prod != null) prod.StockActual += cantBaseRev;
                }

                // Ajustar crédito si aplica
                if (venta.TipoVenta == TipoVentaConsts.Credito && venta.ClienteCreditoId.HasValue)
                {
                    var cliente = await ctx.ClientesCredito.FindAsync(venta.ClienteCreditoId.Value);
                    if (cliente != null)
                    {
                        cliente.DeudaActual -= venta.Total;
                        if (cliente.DeudaActual < 0) cliente.DeudaActual = 0;
                    }
                }

                ctx.DetallesVenta.RemoveRange(venta.Detalles);
                venta.Total             = 0;
                venta.FueModificada     = true;
                venta.FechaModificacion = DateTime.Now;

                var auditoria = new VentaAuditoria
                {
                    VentaId           = ventaId,
                    FechaModificacion = DateTime.Now,
                    UsuarioModifica   = usuarioModifica,
                    Descripcion       = $"Anulación Total: {motivo}",
                    ValorAnterior     = datosPreviosJson,
                    ValorNuevo        = "{\"Total\": 0, \"Estado\": \"Anulada\"}"
                };
                await ctx.VentasAuditoria.AddAsync(auditoria);

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Error al anular venta: {ex.Message}");
            }
        }

        public List<VentaAuditoria> ObtenerAuditoria(int ventaId)
        {
            using var ctx = new AppDbContext();
            return ctx.VentasAuditoria
                      .Where(a => a.VentaId == ventaId)
                      .OrderByDescending(a => a.FechaModificacion)
                      .ToList();
        }
    }
}
