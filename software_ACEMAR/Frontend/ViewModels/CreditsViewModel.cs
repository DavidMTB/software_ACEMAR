using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class CreditsViewModel : ViewModelBase
    {
        private readonly ICreditService _creditService;
        private readonly IAuthService _authService;

        private ClienteCredito? _clienteSeleccionado;
        private string _montoAbonoTexto = string.Empty;
        private string _observacionAbono = string.Empty;
        private decimal _totalDeudaPorCobrar;
        private int _totalClientesConDeuda;
        private string _mensajePermisoVendedor = string.Empty;
        private bool _tieneAcceso = true;

        // Formulario Nuevo Cliente
        private string _nuevoClienteNombre = string.Empty;
        private string _nuevoClienteDocumento = string.Empty;
        private string _nuevoClienteTelefono = string.Empty;
        private string _nuevoClienteLimite = "1000.00";

        // Formulario Nuevo Fiado Directo
        private string _montoNuevoFiado = string.Empty;
        private string _detalleNuevoFiado = "Materiales varios de ferretería";

        public ObservableCollection<ClienteCredito> ClientesDeuda { get; } = new();

        public ICommand RegistrarAbonoCommand { get; }
        public ICommand RecargarCommand { get; }
        public ICommand RegistrarNuevoClienteCommand { get; }
        public ICommand GuardarEdicionClienteCommand { get; }
        public ICommand EliminarClienteCommand { get; }
        public ICommand RegistrarNuevoFiadoCommand { get; }

        public CreditsViewModel(ICreditService creditService, IAuthService authService)
        {
            _creditService = creditService;
            _authService = authService;

            RegistrarAbonoCommand = new RelayCommand(EjecutarRegistroAbono);
            RecargarCommand = new RelayCommand(CargarDatos);
            RegistrarNuevoClienteCommand = new RelayCommand(EjecutarRegistroNuevoCliente);
            GuardarEdicionClienteCommand = new RelayCommand(EjecutarGuardarEdicionCliente);
            EliminarClienteCommand = new RelayCommand(EjecutarEliminarCliente);
            RegistrarNuevoFiadoCommand = new RelayCommand(EjecutarRegistroNuevoFiado);

            _authService.OnUsuarioCambiado += OnUsuarioCambiado;
            VerificarPermisos();
        }

        public ClienteCredito? ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                if (SetField(ref _clienteSeleccionado, value) && value != null)
                {
                    CargarClienteEnEdicion(value);
                }
            }
        }

        public string MontoAbonoTexto { get => _montoAbonoTexto; set => SetField(ref _montoAbonoTexto, value); }
        public string ObservacionAbono { get => _observacionAbono; set => SetField(ref _observacionAbono, value); }
        public decimal TotalDeudaPorCobrar { get => _totalDeudaPorCobrar; set => SetField(ref _totalDeudaPorCobrar, value); }
        public int TotalClientesConDeuda { get => _totalClientesConDeuda; set => SetField(ref _totalClientesConDeuda, value); }
        public bool TieneAcceso { get => _tieneAcceso; set => SetField(ref _tieneAcceso, value); }
        public string MensajePermisoVendedor { get => _mensajePermisoVendedor; set => SetField(ref _mensajePermisoVendedor, value); }

        public string NuevoClienteNombre { get => _nuevoClienteNombre; set => SetField(ref _nuevoClienteNombre, value); }
        public string NuevoClienteDocumento { get => _nuevoClienteDocumento; set => SetField(ref _nuevoClienteDocumento, value); }
        public string NuevoClienteTelefono { get => _nuevoClienteTelefono; set => SetField(ref _nuevoClienteTelefono, value); }
        public string NuevoClienteLimite { get => _nuevoClienteLimite; set => SetField(ref _nuevoClienteLimite, value); }

        public string MontoNuevoFiado { get => _montoNuevoFiado; set => SetField(ref _montoNuevoFiado, value); }
        public string DetalleNuevoFiado { get => _detalleNuevoFiado; set => SetField(ref _detalleNuevoFiado, value); }

        private void OnUsuarioCambiado()
        {
            VerificarPermisos();
        }

        public void VerificarPermisos()
        {
            if (!_authService.PuedeAccederCreditos(out string mensajeError))
            {
                TieneAcceso = false;
                MensajePermisoVendedor = mensajeError;
                ClientesDeuda.Clear();
                TotalDeudaPorCobrar = 0;
                TotalClientesConDeuda = 0;
            }
            else
            {
                TieneAcceso = true;
                MensajePermisoVendedor = string.Empty;
                CargarDatos();
            }
        }

        public void CargarDatos()
        {
            if (!TieneAcceso) return;

            var lista = _creditService.ObtenerTodos();
            ClientesDeuda.Clear();
            foreach (var c in lista)
            {
                ClientesDeuda.Add(c);
            }

            TotalDeudaPorCobrar = Math.Round(_creditService.ObtenerTotalDeudaCobrar(), 2);
            TotalClientesConDeuda = ClientesDeuda.Count(c => c.DeudaActual > 0);
        }

        private void EjecutarRegistroAbono()
        {
            if (!_authService.PuedeAccederCreditos(out string errorPermiso))
            {
                MessageBox.Show(errorPermiso, "Seguridad de Créditos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ClienteSeleccionado == null)
            {
                MessageBox.Show("Seleccione un cliente de la lista para registrar el abono.", "Abono de Crédito", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!decimal.TryParse(MontoAbonoTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingrese un monto de abono numérico válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string usuario = _authService.UsuarioActual.NombreCompleto;
            if (!_creditService.RegistrarAbono(ClienteSeleccionado.Id, monto, ObservacionAbono, usuario, out string errorAbono))
            {
                MessageBox.Show(errorAbono, "Error en Abono", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Abono de S/ {monto:N2} registrado con éxito!\nLa deuda del cliente '{ClienteSeleccionado.Nombre}' ha sido actualizada.", "Abono Confirmado", MessageBoxButton.OK, MessageBoxImage.Information);

            MontoAbonoTexto = string.Empty;
            ObservacionAbono = string.Empty;
            CargarDatos();
        }

        private void EjecutarRegistroNuevoCliente()
        {
            if (!_authService.PuedeAccederCreditos(out string errorPermiso))
            {
                MessageBox.Show(errorPermiso, "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NuevoClienteNombre) || string.IsNullOrWhiteSpace(NuevoClienteDocumento))
            {
                MessageBox.Show("El nombre y el número de documento (DNI/RUC) son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(NuevoClienteLimite.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal limite) || limite <= 0)
            {
                MessageBox.Show("Ingrese un límite de crédito válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nuevoCliente = new ClienteCredito
            {
                Nombre = NuevoClienteNombre.Trim(),
                Documento = NuevoClienteDocumento.Trim(),
                Telefono = NuevoClienteTelefono.Trim(),
                LimiteCredito = limite,
                DeudaActual = 0m,
                UltimaActualizacion = DateTime.Now
            };

            if (!_creditService.CrearCliente(nuevoCliente, out string error))
            {
                MessageBox.Show(error, "Error al Crear Cliente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Cliente '{nuevoCliente.Nombre}' registrado exitosamente en la cartera de créditos!", "Cliente Creado", MessageBoxButton.OK, MessageBoxImage.Information);

            NuevoClienteNombre = string.Empty;
            NuevoClienteDocumento = string.Empty;
            NuevoClienteTelefono = string.Empty;
            NuevoClienteLimite = "1000.00";
            CargarDatos();
        }

        private void EjecutarRegistroNuevoFiado()
        {
            if (!_authService.PuedeAccederCreditos(out string errorPermiso))
            {
                MessageBox.Show(errorPermiso, "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ClienteSeleccionado == null)
            {
                MessageBox.Show("Seleccione un cliente de la lista para otorgarle el nuevo fiado.", "Nuevo Fiado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!decimal.TryParse(MontoNuevoFiado.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingrese un monto de fiado numérico válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string usuario = _authService.UsuarioActual.NombreCompleto;
            if (!_creditService.RegistrarNuevoFiado(ClienteSeleccionado.Id, monto, DetalleNuevoFiado, usuario, out string error))
            {
                MessageBox.Show(error, "No se pudo registrar el fiado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Nuevo fiado por S/ {monto:N2} otorgado con éxito a '{ClienteSeleccionado.Nombre}'!", "Fiado Registrado", MessageBoxButton.OK, MessageBoxImage.Information);

            MontoNuevoFiado = string.Empty;
            DetalleNuevoFiado = "Materiales varios de ferretería";
            CargarDatos();
        }

        private void CargarClienteEnEdicion(ClienteCredito c)
        {
            NuevoClienteNombre = c.Nombre;
            NuevoClienteDocumento = c.Documento;
            NuevoClienteTelefono = c.Telefono ?? string.Empty;
            NuevoClienteLimite = c.LimiteCredito.ToString("F2");
        }

        private void EjecutarGuardarEdicionCliente()
        {
            if (!_authService.PuedeAccederCreditos(out string errorPermiso))
            {
                MessageBox.Show(errorPermiso, "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ClienteSeleccionado == null)
            {
                MessageBox.Show("Seleccione un cliente de la lista para modificar sus datos.", "Editar Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(NuevoClienteNombre))
            {
                MessageBox.Show("El nombre del cliente no puede estar vacío.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(NuevoClienteLimite.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal limite) || limite <= 0)
            {
                MessageBox.Show("Ingrese un límite de crédito válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clienteEditado = new ClienteCredito
            {
                Id = ClienteSeleccionado.Id,
                Nombre = NuevoClienteNombre.Trim(),
                Documento = ClienteSeleccionado.Documento,
                Telefono = NuevoClienteTelefono.Trim(),
                LimiteCredito = limite
            };

            if (!_creditService.ActualizarCliente(clienteEditado, out string error))
            {
                MessageBox.Show(error, "Error al Actualizar Cliente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Datos de '{clienteEditado.Nombre}' actualizados con éxito!\nNuevo límite de crédito: S/ {limite:N2}", "Cliente Actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarDatos();
        }

        private void EjecutarEliminarCliente(object? param)
        {
            var cliente = param as ClienteCredito ?? ClienteSeleccionado;
            if (cliente == null)
            {
                MessageBox.Show("Seleccione un cliente o empresa de la lista para eliminar.", "Eliminar Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_authService.PuedeAccederCreditos(out string errorPermiso))
            {
                MessageBox.Show(errorPermiso, "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string mensajeConfirm = cliente.DeudaActual > 0
                ? $"⚠️ ATENCIÓN: El cliente/empresa '{cliente.Nombre}' tiene una deuda pendiente de S/ {cliente.DeudaActual:N2}.\n\n¿Está seguro de eliminar esta cuenta corriente de la base de datos?"
                : $"¿Desea eliminar definitivamente el registro del cliente / empresa '{cliente.Nombre}'?";

            var confirmacion = MessageBox.Show(mensajeConfirm, "Confirmar Eliminación de Cliente", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmacion != MessageBoxResult.Yes) return;

            if (!_creditService.EliminarCliente(cliente.Id, out string error))
            {
                MessageBox.Show(error, "Error al Eliminar Cliente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Cliente / Empresa '{cliente.Nombre}' eliminado con éxito.", "Cliente Eliminado", MessageBoxButton.OK, MessageBoxImage.Information);
            ClienteSeleccionado = null;
            LimpiarFormularioCliente();
            CargarDatos();
        }

        private void LimpiarFormularioCliente()
        {
            NuevoClienteNombre = string.Empty;
            NuevoClienteDocumento = string.Empty;
            NuevoClienteTelefono = string.Empty;
            NuevoClienteLimite = "1000.00";
        }
    }
}
