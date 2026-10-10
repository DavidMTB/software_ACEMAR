using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class LotesViewModel : ViewModelBase
    {
        private readonly ILoteService _loteService;
        private readonly IInventoryService _inventoryService;
        private readonly IProveedorService _proveedorService;

        private Producto? _productoFiltro;
        private LoteProducto? _loteSeleccionado;

        // Formulario de Lote
        private int _formId;
        private Producto? _formProductoSeleccionado;
        private Proveedor? _formProveedorSeleccionado;
        private string _formCantidadInicial = "100";
        private string _formCostoCompra = "5.00";
        private string _formPrecioVenta = "7.50";
        private DateTime _formFechaIngreso = DateTime.Now;
        private string _formProveedorNombre = "Proveedor General";
        private string _formUbicacionAlmacen = "Almacén Principal - Estante A1";

        public ObservableCollection<Producto> ProductosDisponibles { get; } = new();
        public ObservableCollection<Proveedor> ProveedoresDisponibles { get; } = new();
        public ObservableCollection<LoteProducto> Lotes { get; } = new();

        public ICommand RegistrarLoteCommand { get; }
        public ICommand DesactivarLoteCommand { get; }
        public ICommand LimpiarFormularioCommand { get; }
        public ICommand RefrescarLotesCommand { get; }

        public LotesViewModel(ILoteService loteService, IInventoryService inventoryService, IProveedorService? proveedorService = null)
        {
            _loteService = loteService;
            _inventoryService = inventoryService;
            _proveedorService = proveedorService ?? new ProveedorService();

            RegistrarLoteCommand = new RelayCommand(EjecutarRegistrarLote);
            DesactivarLoteCommand = new RelayCommand(EjecutarDesactivarLote);
            LimpiarFormularioCommand = new RelayCommand(LimpiarFormulario);
            RefrescarLotesCommand = new RelayCommand(() => { CargarProveedores(); CargarLotes(); });

            CargarProductos();
            CargarProveedores();
            CargarLotes();
        }

        public Producto? ProductoFiltro
        {
            get => _productoFiltro;
            set
            {
                if (SetField(ref _productoFiltro, value))
                {
                    CargarLotes();
                }
            }
        }

        public LoteProducto? LoteSeleccionado
        {
            get => _loteSeleccionado;
            set => SetField(ref _loteSeleccionado, value);
        }

        public int FormId { get => _formId; set => SetField(ref _formId, value); }
        public Producto? FormProductoSeleccionado
        {
            get => _formProductoSeleccionado;
            set
            {
                if (SetField(ref _formProductoSeleccionado, value) && value != null)
                {
                    decimal precioVenta = _loteService.ObtenerPrecioVigenteProducto(value.Id);
                    if (precioVenta <= 0) precioVenta = value.PrecioUnitario;

                    FormPrecioVenta = precioVenta > 0 ? precioVenta.ToString("0.00") : "10.00";
                    FormCostoCompra = precioVenta > 0 ? Math.Round(precioVenta * 0.70m, 2).ToString("0.00") : "7.00";

                    // Autoseleccionar proveedor asignado al producto si existe
                    var provs = _proveedorService.ObtenerProveedoresDeProducto(value.Id);
                    if (provs.Any())
                    {
                        var coincidencia = ProveedoresDisponibles.FirstOrDefault(p => provs.Any(pr => pr.Id == p.Id));
                        if (coincidencia != null)
                        {
                            FormProveedorSeleccionado = coincidencia;
                        }
                    }
                }
            }
        }

        public Proveedor? FormProveedorSeleccionado
        {
            get => _formProveedorSeleccionado;
            set => SetField(ref _formProveedorSeleccionado, value);
        }

        public string FormCantidadInicial { get => _formCantidadInicial; set => SetField(ref _formCantidadInicial, value); }
        public string FormCostoCompra { get => _formCostoCompra; set => SetField(ref _formCostoCompra, value); }
        public string FormPrecioVenta { get => _formPrecioVenta; set => SetField(ref _formPrecioVenta, value); }
        public DateTime FormFechaIngreso { get => _formFechaIngreso; set => SetField(ref _formFechaIngreso, value); }
        public string FormProveedorNombre { get => _formProveedorNombre; set => SetField(ref _formProveedorNombre, value); }
        public string FormUbicacionAlmacen { get => _formUbicacionAlmacen; set => SetField(ref _formUbicacionAlmacen, value); }

        public void CargarProductos()
        {
            ProductosDisponibles.Clear();
            foreach (var p in _inventoryService.ObtenerTodos())
            {
                ProductosDisponibles.Add(p);
            }
        }

        public void CargarProveedores()
        {
            ProveedoresDisponibles.Clear();
            var lista = _proveedorService.ObtenerTodos();
            foreach (var p in lista)
            {
                ProveedoresDisponibles.Add(p);
            }
            if (FormProveedorSeleccionado == null && ProveedoresDisponibles.Any())
            {
                FormProveedorSeleccionado = ProveedoresDisponibles.First();
            }
        }

        public void CargarLotes()
        {
            Lotes.Clear();
            var lista = ProductoFiltro != null 
                ? _loteService.ObtenerLotesPorProducto(ProductoFiltro.Id) 
                : _loteService.ObtenerTodosLotes();

            foreach (var l in lista)
            {
                Lotes.Add(l);
            }
        }

        private void LimpiarFormulario()
        {
            FormId = 0;
            FormProductoSeleccionado = null;
            FormProveedorSeleccionado = ProveedoresDisponibles.FirstOrDefault();
            FormCantidadInicial = "100";
            FormCostoCompra = "5.00";
            FormPrecioVenta = "7.50";
            FormFechaIngreso = DateTime.Now;
            FormProveedorNombre = "Proveedor General";
            FormUbicacionAlmacen = "Almacén Principal - Estante A1";
        }

        private void EjecutarRegistrarLote()
        {
            if (FormProductoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto para asignar el lote.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(FormCantidadInicial.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingrese una cantidad inicial válida mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(FormCostoCompra.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal costo) || costo < 0)
            {
                MessageBox.Show("Ingrese un costo de compra válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(FormPrecioVenta.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precioVenta) || precioVenta <= 0)
            {
                precioVenta = FormProductoSeleccionado.PrecioUnitario > 0 ? FormProductoSeleccionado.PrecioUnitario : Math.Round(costo * 1.25m, 2);
            }

            string nombreProv = FormProveedorSeleccionado?.Nombre ?? (string.IsNullOrWhiteSpace(FormProveedorNombre) ? "Proveedor General" : FormProveedorNombre.Trim());

            var lote = new LoteProducto
            {
                ProductoId = FormProductoSeleccionado.Id,
                ProveedorId = FormProveedorSeleccionado?.Id,
                CantidadInicial = cantidad,
                CantidadDisponible = cantidad,
                CostoCompra = costo,
                PrecioVenta = precioVenta,
                FechaIngreso = FormFechaIngreso,
                UnidadMedida = FormProductoSeleccionado.UnidadMedida ?? "Unidad",
                Observaciones = $"Proveedor: {nombreProv} | Ubicación: {FormUbicacionAlmacen.Trim()}",
                Estado = "Activo"
            };

            var (exito, error) = _loteService.CrearLoteAsync(lote).GetAwaiter().GetResult();
            if (!exito)
            {
                MessageBox.Show(error, "Error al Registrar Lote", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Lote N° {lote.Id} registrado e integrado al stock del producto '{FormProductoSeleccionado.Nombre}'.", "Lote Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            LimpiarFormulario();
            CargarLotes();
            CargarProductos();
        }

        private void EjecutarDesactivarLote(object? param)
        {
            var lote = param as LoteProducto ?? LoteSeleccionado;
            if (lote == null)
            {
                MessageBox.Show("Seleccione un lote para dar de baja.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"¿Desea cerrar/agotar el lote N° {lote.Id}? El stock remanente ({lote.CantidadDisponible:N2}) ya no estará disponible para venta FIFO.", "Dar de Baja Lote", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            lote.CantidadDisponible = 0;
            lote.Estado = "Agotado";
            MessageBox.Show("Lote marcado como agotado.", "Operación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarLotes();
            CargarProductos();
        }
    }
}
