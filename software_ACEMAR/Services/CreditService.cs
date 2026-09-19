using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface ICreditService
    {
        List<ClienteCredito> ObtenerClientesConDeuda();
        List<ClienteCredito> ObtenerTodos();
        ClienteCredito? ObtenerPorId(int id);
        bool RegistrarAbono(int clienteId, decimal montoAbono, string observaciones, string usuario, out string mensajeError);
        decimal ObtenerTotalDeudaCobrar();

        // Nuevas funciones solicitadas: Crear cliente y registrar nuevo fiado
        bool CrearCliente(ClienteCredito cliente, out string error);
        bool RegistrarNuevoFiado(int clienteId, decimal monto, string detalle, string usuario, out string error);
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
            using var context = new AppDbContext();
            return context.ClientesCredito
                .Include(c => c.Abonos)
                .Where(c => c.DeudaActual > 0)
                .AsEnumerable()
                .OrderByDescending(c => c.DeudaActual)
                .ToList();
        }

        public List<ClienteCredito> ObtenerTodos()
        {
            using var context = new AppDbContext();
            return context.ClientesCredito
                .Include(c => c.Abonos)
                .AsEnumerable()
                .OrderBy(c => c.Nombre)
                .ToList();
        }

        public ClienteCredito? ObtenerPorId(int id)
        {
            using var context = new AppDbContext();
            return context.ClientesCredito
                .Include(c => c.Abonos)
                .FirstOrDefault(c => c.Id == id);
        }

        public bool RegistrarAbono(int clienteId, decimal montoAbono, string observaciones, string usuario, out string mensajeError)
        {
            mensajeError = string.Empty;

            // Regla CRE-01 (Seguridad Fiados):
            // Si un usuario con rol "Vendedor" intenta abrir la vista de créditos o procesar uno,
            // bloquear acceso con el mensaje: "Permisos insuficientes. Contacte al Administrador".
            if (!_authService.PuedeAccederCreditos(out mensajeError))
            {
                return false;
            }

            if (montoAbono <= 0)
            {
                mensajeError = "El monto del abono debe ser mayor a cero.";
                return false;
            }

            using var context = new AppDbContext();
            var cliente = context.ClientesCredito.FirstOrDefault(c => c.Id == clienteId);
            if (cliente == null)
            {
                mensajeError = "Cliente no encontrado.";
                return false;
            }

            if (montoAbono > cliente.DeudaActual)
            {
                mensajeError = $"El monto del abono (S/ {montoAbono:N2}) no puede exceder la deuda actual (S/ {cliente.DeudaActual:N2}).";
                return false;
            }

            // Resta matemática de la deuda en la BD local
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

            context.AbonosCredito.Add(abono);
            context.SaveChanges();
            return true;
        }

        public decimal ObtenerTotalDeudaCobrar()
        {
            using var context = new AppDbContext();
            return context.ClientesCredito.Select(c => c.DeudaActual).AsEnumerable().Sum();
        }

        public bool CrearCliente(ClienteCredito cliente, out string error)
        {
            error = string.Empty;

            if (!_authService.PuedeAccederCreditos(out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(cliente.Nombre))
            {
                error = "El nombre o razón social del cliente es obligatorio.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(cliente.Documento))
            {
                error = "El número de documento (DNI o RUC) es obligatorio.";
                return false;
            }
            if (cliente.LimiteCredito <= 0)
            {
                error = "El límite de crédito aprobado debe ser mayor a cero.";
                return false;
            }

            using var context = new AppDbContext();
            if (context.ClientesCredito.Any(c => c.Documento.Trim() == cliente.Documento.Trim()))
            {
                error = $"Ya existe un cliente registrado con el documento {cliente.Documento}.";
                return false;
            }

            cliente.Nombre = cliente.Nombre.Trim();
            cliente.Documento = cliente.Documento.Trim();
            cliente.Telefono = cliente.Telefono?.Trim() ?? string.Empty;
            cliente.DeudaActual = Math.Max(0, cliente.DeudaActual);
            cliente.UltimaActualizacion = DateTime.Now;

            context.ClientesCredito.Add(cliente);
            context.SaveChanges();
            return true;
        }

        public bool RegistrarNuevoFiado(int clienteId, decimal monto, string detalle, string usuario, out string error)
        {
            error = string.Empty;

            if (!_authService.PuedeAccederCreditos(out error))
            {
                return false;
            }

            if (monto <= 0)
            {
                error = "El monto del nuevo fiado debe ser mayor a cero.";
                return false;
            }

            using var context = new AppDbContext();
            var cliente = context.ClientesCredito.FirstOrDefault(c => c.Id == clienteId);
            if (cliente == null)
            {
                error = "Cliente no encontrado.";
                return false;
            }

            // Validar límite de crédito
            decimal nuevaDeuda = cliente.DeudaActual + monto;
            if (nuevaDeuda > cliente.LimiteCredito)
            {
                error = $"Límite de crédito excedido. Disponible: S/ {cliente.CreditoDisponible:N2}. Monto solicitado: S/ {monto:N2}.";
                return false;
            }

            cliente.DeudaActual = Math.Round(nuevaDeuda, 2);
            cliente.UltimaActualizacion = DateTime.Now;

            // Registrar la transacción como venta a crédito para arqueo de caja
            var ventaCredito = new Venta
            {
                Fecha = DateTime.Now,
                Total = monto,
                TipoVenta = "Credito",
                ClienteCreditoId = clienteId,
                ClienteNombre = cliente.Nombre,
                UsuarioVendedor = string.IsNullOrWhiteSpace(usuario) ? _authService.UsuarioActual.NombreCompleto : usuario,
                NumeroTicket = $"FIA-{DateTime.Now:yyyyMMdd}-{context.Ventas.Count() + 1:D4}"
            };

            context.Ventas.Add(ventaCredito);
            context.SaveChanges();
            return true;
        }
    }
}
