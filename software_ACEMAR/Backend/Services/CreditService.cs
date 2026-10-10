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
    public interface ICreditService
    {
        List<ClienteCredito> ObtenerClientesConDeuda();
        Task<List<ClienteCredito>> ObtenerClientesConDeudaAsync(CancellationToken cancellationToken = default);

        List<ClienteCredito> ObtenerTodos();
        Task<List<ClienteCredito>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

        ClienteCredito? ObtenerPorId(int id);
        Task<ClienteCredito?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

        bool RegistrarAbono(int clienteId, decimal montoAbono, string observaciones, string usuario, out string mensajeError);
        Task<(bool Exito, string MensajeError)> RegistrarAbonoAsync(int clienteId, decimal montoAbono, string observaciones, string usuario, CancellationToken cancellationToken = default);

        decimal ObtenerTotalDeudaCobrar();
        Task<decimal> ObtenerTotalDeudaCobrarAsync(CancellationToken cancellationToken = default);

        bool CrearCliente(ClienteCredito cliente, out string error);
        Task<(bool Exito, string Error)> CrearClienteAsync(ClienteCredito cliente, CancellationToken cancellationToken = default);

        bool ActualizarCliente(ClienteCredito cliente, out string error);
        Task<(bool Exito, string Error)> ActualizarClienteAsync(ClienteCredito cliente, CancellationToken cancellationToken = default);

        bool RegistrarNuevoFiado(int clienteId, decimal monto, string detalle, string usuario, out string error);
        Task<(bool Exito, string Error)> RegistrarNuevoFiadoAsync(int clienteId, decimal monto, string detalle, string usuario, CancellationToken cancellationToken = default);

        bool EliminarCliente(int clienteId, out string error);
        Task<(bool Exito, string Error)> EliminarClienteAsync(int clienteId, CancellationToken cancellationToken = default);
    }

    public class CreditService : ICreditService
    {
        private readonly IAuthService _authService;

        public CreditService(IAuthService authService)
        {
            _authService = authService;
        }

        public List<ClienteCredito> ObtenerClientesConDeuda()
        {
            return ObtenerClientesConDeudaAsync().GetAwaiter().GetResult();
        }

        public async Task<List<ClienteCredito>> ObtenerClientesConDeudaAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.ClientesCredito
                .AsNoTracking()
                .Include(c => c.Abonos)
                .Where(c => c.DeudaActual > 0)
                .OrderByDescending(c => c.DeudaActual)
                .ToListAsync(cancellationToken);
        }

        public List<ClienteCredito> ObtenerTodos()
        {
            return ObtenerTodosAsync().GetAwaiter().GetResult();
        }

        public async Task<List<ClienteCredito>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.ClientesCredito
                .AsNoTracking()
                .Include(c => c.Abonos)
                .OrderBy(c => c.Nombre)
                .ToListAsync(cancellationToken);
        }

        public ClienteCredito? ObtenerPorId(int id)
        {
            return ObtenerPorIdAsync(id).GetAwaiter().GetResult();
        }

        public async Task<ClienteCredito?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.ClientesCredito
                .AsNoTracking()
                .Include(c => c.Abonos)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public bool RegistrarAbono(int clienteId, decimal montoAbono, string observaciones, string usuario, out string mensajeError)
        {
            var (exito, error) = RegistrarAbonoAsync(clienteId, montoAbono, observaciones, usuario).GetAwaiter().GetResult();
            mensajeError = error;
            return exito;
        }

        public async Task<(bool Exito, string MensajeError)> RegistrarAbonoAsync(int clienteId, decimal montoAbono, string observaciones, string usuario, CancellationToken cancellationToken = default)
        {
            // Regla CRE-01 (Seguridad Fiados)
            if (!_authService.PuedeAccederCreditos(out string permisoError))
            {
                return (false, permisoError);
            }

            if (montoAbono <= 0)
            {
                return (false, "El monto del abono debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var cliente = await context.ClientesCredito.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);
                if (cliente == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (false, "Cliente no encontrado.");
                }

                if (montoAbono > cliente.DeudaActual)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (false, $"El monto del abono (S/ {montoAbono:N2}) no puede exceder la deuda actual (S/ {cliente.DeudaActual:N2}).");
                }

                // Resta matemática de la deuda en la BD local dentro de transacción atómica
                cliente.DeudaActual = Math.Round(cliente.DeudaActual - montoAbono, 2);
                cliente.UltimaActualizacion = DateTime.Now;

                var abono = new AbonoCredito
                {
                    ClienteCreditoId = clienteId,
                    Monto = montoAbono,
                    Fecha = DateTime.Now,
                    Observaciones = string.IsNullOrWhiteSpace(observaciones) ? "Abono a cuenta en mostrador" : observaciones.Trim(),
                    UsuarioRegistra = string.IsNullOrWhiteSpace(usuario) ? _authService.UsuarioActual.NombreUsuario : usuario
                };

                await context.AbonosCredito.AddAsync(abono, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Error al registrar el abono: {ex.Message}");
            }
        }

        public decimal ObtenerTotalDeudaCobrar()
        {
            return ObtenerTotalDeudaCobrarAsync().GetAwaiter().GetResult();
        }

        public async Task<decimal> ObtenerTotalDeudaCobrarAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            var deudas = await context.ClientesCredito
                .AsNoTracking()
                .Select(c => c.DeudaActual)
                .ToListAsync(cancellationToken);
            return deudas.Sum();
        }

        public bool CrearCliente(ClienteCredito cliente, out string error)
        {
            var (exito, err) = CrearClienteAsync(cliente).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> CrearClienteAsync(ClienteCredito cliente, CancellationToken cancellationToken = default)
        {
            if (!_authService.PuedeAccederCreditos(out string permError))
            {
                return (false, permError);
            }

            if (string.IsNullOrWhiteSpace(cliente.Nombre))
            {
                return (false, "El nombre o razón social del cliente es obligatorio.");
            }
            if (string.IsNullOrWhiteSpace(cliente.Documento))
            {
                return (false, "El número de documento (DNI o RUC) es obligatorio.");
            }
            if (cliente.LimiteCredito <= 0)
            {
                return (false, "El límite de crédito aprobado debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            bool existeDoc = await context.ClientesCredito
                .AsNoTracking()
                .AnyAsync(c => c.Documento.Trim() == cliente.Documento.Trim(), cancellationToken);

            if (existeDoc)
            {
                return (false, $"Ya existe un cliente registrado con el documento {cliente.Documento}.");
            }

            cliente.Nombre = cliente.Nombre.Trim();
            cliente.Documento = cliente.Documento.Trim();
            cliente.Telefono = cliente.Telefono?.Trim() ?? string.Empty;
            cliente.DeudaActual = Math.Max(0, cliente.DeudaActual);
            cliente.UltimaActualizacion = DateTime.Now;

            await context.ClientesCredito.AddAsync(cliente, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public bool ActualizarCliente(ClienteCredito cliente, out string error)
        {
            var (exito, err) = ActualizarClienteAsync(cliente).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> ActualizarClienteAsync(ClienteCredito cliente, CancellationToken cancellationToken = default)
        {
            if (!_authService.PuedeAccederCreditos(out string permError))
            {
                return (false, permError);
            }

            if (string.IsNullOrWhiteSpace(cliente.Nombre))
            {
                return (false, "El nombre o razón social del cliente es obligatorio.");
            }
            if (cliente.LimiteCredito <= 0)
            {
                return (false, "El límite de crédito aprobado debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            var dbCliente = await context.ClientesCredito
                .FirstOrDefaultAsync(c => c.Id == cliente.Id, cancellationToken);

            if (dbCliente == null)
            {
                return (false, "Cliente no encontrado en la base de datos.");
            }

            dbCliente.Nombre = cliente.Nombre.Trim();
            dbCliente.Telefono = cliente.Telefono?.Trim() ?? string.Empty;
            dbCliente.LimiteCredito = cliente.LimiteCredito;
            dbCliente.UltimaActualizacion = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }

        public bool RegistrarNuevoFiado(int clienteId, decimal monto, string detalle, string usuario, out string error)
        {
            var (exito, err) = RegistrarNuevoFiadoAsync(clienteId, monto, detalle, usuario).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> RegistrarNuevoFiadoAsync(int clienteId, decimal monto, string detalle, string usuario, CancellationToken cancellationToken = default)
        {
            if (!_authService.PuedeAccederCreditos(out string permError))
            {
                return (false, permError);
            }

            if (monto <= 0)
            {
                return (false, "El monto del nuevo fiado debe ser mayor a cero.");
            }

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var cliente = await context.ClientesCredito.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);
                if (cliente == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (false, "Cliente no encontrado.");
                }

                // Validar límite de crédito
                decimal nuevaDeuda = cliente.DeudaActual + monto;
                if (nuevaDeuda > cliente.LimiteCredito)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (false, $"Límite de crédito excedido. Disponible: S/ {cliente.CreditoDisponible:N2}. Monto solicitado: S/ {monto:N2}.");
                }

                cliente.DeudaActual = Math.Round(nuevaDeuda, 2);
                cliente.UltimaActualizacion = DateTime.Now;

                // Optimización de secuencia de ticket
                var maxId = await context.Ventas.MaxAsync(v => (int?)v.Id, cancellationToken) ?? 0;

                // Registrar la transacción como venta a crédito para arqueo de caja
                var ventaCredito = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = monto,
                    TipoVenta = TipoVentaConsts.Credito,
                    ClienteCreditoId = clienteId,
                    ClienteNombre = cliente.Nombre,
                    UsuarioVendedor = string.IsNullOrWhiteSpace(usuario) ? _authService.UsuarioActual.NombreCompleto : usuario,
                    NumeroTicket = $"FIA-{DateTime.Now:yyyyMMdd}-{(maxId + 1):D4}"
                };

                await context.Ventas.AddAsync(ventaCredito, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Error al registrar nuevo fiado: {ex.Message}");
            }
        }

        public bool EliminarCliente(int clienteId, out string error)
        {
            var (exito, err) = EliminarClienteAsync(clienteId).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> EliminarClienteAsync(int clienteId, CancellationToken cancellationToken = default)
        {
            if (!_authService.PuedeAccederCreditos(out string permError))
            {
                return (false, permError);
            }

            await using var context = new AppDbContext();
            var cliente = await context.ClientesCredito
                .Include(c => c.Abonos)
                .FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);

            if (cliente == null)
            {
                return (false, "Cliente no encontrado.");
            }

            if (cliente.Abonos.Any())
            {
                context.AbonosCredito.RemoveRange(cliente.Abonos);
            }

            context.ClientesCredito.Remove(cliente);
            await context.SaveChangesAsync(cancellationToken);
            return (true, string.Empty);
        }
    }
}
