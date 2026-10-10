using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class ProveedoresViewModel : ViewModelBase
    {
        private readonly IProveedorService _proveedorService;
        private readonly IInventoryService _inventoryService;

        private Proveedor? _proveedorSeleccionado;
        private string _busquedaTexto = string.Empty;

        // Formulario
        private int _formId;
        private string _formNombre = string.Empty;
        private string _formContacto = string.Empty;
        private string _formTelefono = string.Empty;
        private string _formCorreo = string.Empty;
        private string _formDireccion = string.Empty;
        private string _formObservaciones = string.Empty;
        private bool _esModoEdicion;

        public ObservableCollection<Proveedor> Proveedores { get; } = new();

        public ICommand GuardarProveedorCommand { get; }
        public ICommand EditarProveedorCommand { get; }
        public ICommand EliminarProveedorCommand { get; }
        public ICommand LimpiarFormularioCommand { get; }
        public ICommand RefrescarCommand { get; }

        public ProveedoresViewModel(IProveedorService proveedorService, IInventoryService inventoryService)
        {
            _proveedorService = proveedorService;
            _inventoryService = inventoryService;

            GuardarProveedorCommand = new RelayCommand(EjecutarGuardarProveedor);
            EditarProveedorCommand = new RelayCommand(EjecutarEditarProveedor);
            EliminarProveedorCommand = new RelayCommand(EjecutarEliminarProveedor);
            LimpiarFormularioCommand = new RelayCommand(LimpiarFormulario);
            RefrescarCommand = new RelayCommand(CargarProveedores);

            CargarProveedores();
        }

        public string BusquedaTexto
        {
            get => _busquedaTexto;
            set
            {
                if (SetField(ref _busquedaTexto, value))
                {
                    CargarProveedores();
                }
            }
        }

        public Proveedor? ProveedorSeleccionado
        {
            get => _proveedorSeleccionado;
            set
            {
                if (SetField(ref _proveedorSeleccionado, value) && value != null)
                {
                    CargarEnFormulario(value);
                }
            }
        }

        public int FormId { get => _formId; set => SetField(ref _formId, value); }
        public string FormNombre { get => _formNombre; set => SetField(ref _formNombre, value); }
        public string FormContacto { get => _formContacto; set => SetField(ref _formContacto, value); }
        public string FormTelefono { get => _formTelefono; set => SetField(ref _formTelefono, value); }
        public string FormCorreo { get => _formCorreo; set => SetField(ref _formCorreo, value); }
        public string FormDireccion { get => _formDireccion; set => SetField(ref _formDireccion, value); }
        public string FormObservaciones { get => _formObservaciones; set => SetField(ref _formObservaciones, value); }
        public bool EsModoEdicion { get => _esModoEdicion; set => SetField(ref _esModoEdicion, value); }

        public void CargarProveedores()
        {
            Proveedores.Clear();
            var lista = string.IsNullOrWhiteSpace(BusquedaTexto)
                ? _proveedorService.ObtenerTodos()
                : _proveedorService.Buscar(BusquedaTexto);

            foreach (var p in lista)
            {
                Proveedores.Add(p);
            }
        }

        private void CargarEnFormulario(Proveedor p)
        {
            FormId = p.Id;
            FormNombre = p.Nombre;
            FormContacto = p.Contacto ?? string.Empty;
            FormTelefono = p.Telefono ?? string.Empty;
            FormCorreo = p.Correo ?? string.Empty;
            FormDireccion = p.Direccion ?? string.Empty;
            FormObservaciones = p.Observaciones ?? string.Empty;
            EsModoEdicion = true;
        }

        private void LimpiarFormulario()
        {
            FormId = 0;
            FormNombre = string.Empty;
            FormContacto = string.Empty;
            FormTelefono = string.Empty;
            FormCorreo = string.Empty;
            FormDireccion = string.Empty;
            FormObservaciones = string.Empty;
            EsModoEdicion = false;
            ProveedorSeleccionado = null;
        }

        private void EjecutarEditarProveedor(object? param)
        {
            if (param is Proveedor p)
            {
                CargarEnFormulario(p);
            }
        }

        private void EjecutarGuardarProveedor()
        {
            if (string.IsNullOrWhiteSpace(FormNombre))
            {
                MessageBox.Show("El nombre / razón social del proveedor es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var proveedor = new Proveedor
            {
                Id = FormId,
                Nombre = FormNombre.Trim(),
                Contacto = FormContacto.Trim(),
                Telefono = FormTelefono.Trim(),
                Correo = FormCorreo.Trim(),
                Direccion = FormDireccion.Trim(),
                Observaciones = FormObservaciones.Trim()
            };

            bool exito;
            string error;

            if (FormId == 0)
            {
                exito = _proveedorService.Crear(proveedor, out error);
            }
            else
            {
                exito = _proveedorService.Actualizar(proveedor, out error);
            }

            if (!exito)
            {
                MessageBox.Show(error, "Error al Guardar Proveedor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Proveedor '{proveedor.Nombre}' guardado con éxito.", "Operación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            LimpiarFormulario();
            CargarProveedores();
        }

        private void EjecutarEliminarProveedor(object? param)
        {
            var p = param as Proveedor ?? ProveedorSeleccionado;
            if (p == null) return;

            var confirm = MessageBox.Show($"¿Desea eliminar al proveedor '{p.Nombre}'?", "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            if (_proveedorService.Eliminar(p.Id, out string error))
            {
                MessageBox.Show("Proveedor eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                LimpiarFormulario();
                CargarProveedores();
            }
            else
            {
                MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
