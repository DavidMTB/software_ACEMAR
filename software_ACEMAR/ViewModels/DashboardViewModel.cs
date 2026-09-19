using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;

        private Producto? _productoSeleccionado;
        private string _cantidadIngresoTexto = string.Empty;
        private string _filtroTexto = string.Empty;

        // KPIs
        private int _totalProductos;
        private int _totalProductosBajoStock;
        private decimal _valorTotalInventario;
        private decimal _promedioOcupacionCuMax;

        public ObservableCollection<Producto> Inventario { get; } = new();

        public ICommand CargarInventarioCommand { get; }
        public ICommand RegistrarIngresoCommand { get; }
        public ICommand LimpiarFiltroCommand { get; }

        public DashboardViewModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;

            CargarInventarioCommand = new RelayCommand(CargarInventario);
            RegistrarIngresoCommand = new RelayCommand(EjecutarIngresoMercaderia);
            LimpiarFiltroCommand = new RelayCommand(() => FiltroTexto = string.Empty);

            CargarInventario();
        }

        public string FiltroTexto
        {
            get => _filtroTexto;
            set
            {
                if (SetField(ref _filtroTexto, value))
                {
                    FiltrarInventario();
                }
            }
        }

        public Producto? ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set => SetField(ref _productoSeleccionado, value);
        }

        public string CantidadIngresoTexto
        {
            get => _cantidadIngresoTexto;
            set => SetField(ref _cantidadIngresoTexto, value);
        }

        public int TotalProductos
        {
            get => _totalProductos;
            set => SetField(ref _totalProductos, value);
        }

        public int TotalProductosBajoStock
        {
            get => _totalProductosBajoStock;
            set => SetField(ref _totalProductosBajoStock, value);
        }

        public decimal ValorTotalInventario
        {
            get => _valorTotalInventario;
            set => SetField(ref _valorTotalInventario, value);
        }

        public decimal PromedioOcupacionCuMax
        {
            get => _promedioOcupacionCuMax;
            set => SetField(ref _promedioOcupacionCuMax, value);
        }

        public void CargarInventario()
        {
            var todos = _inventoryService.ObtenerTodos();

            Inventario.Clear();
            foreach (var p in todos)
            {
                Inventario.Add(p);
            }

            ActualizarKpis(todos);
        }

        private void FiltrarInventario()
        {
            var filtrados = _inventoryService.BuscarPorNombre(FiltroTexto);
            Inventario.Clear();
            foreach (var p in filtrados)
            {
                Inventario.Add(p);
            }
        }

        private void ActualizarKpis(System.Collections.Generic.List<Producto> productos)
        {
            TotalProductos = productos.Count;
            TotalProductosBajoStock = productos.Count(p => p.EsStockBajo);
            ValorTotalInventario = Math.Round(productos.Sum(p => p.StockActual * p.PrecioUnitario), 2);

            if (productos.Any(p => p.CuMax > 0))
            {
                decimal sumaOcupacion = productos.Where(p => p.CuMax > 0).Sum(p => (p.StockActual / p.CuMax) * 100);
                PromedioOcupacionCuMax = Math.Round(sumaOcupacion / productos.Count(p => p.CuMax > 0), 1);
            }
            else
            {
                PromedioOcupacionCuMax = 0;
            }
        }

        private void EjecutarIngresoMercaderia()
        {
            if (ProductoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto del inventario para registrar el ingreso.", "Ingreso de Mercadería", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!decimal.TryParse(CantidadIngresoTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cantidad)
                && !decimal.TryParse(CantidadIngresoTexto, out cantidad))
            {
                MessageBox.Show("Ingrese una cantidad numérica válida a ingresar.", "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cantidad <= 0)
            {
                MessageBox.Show("La cantidad a ingresar debe ser mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Regla INV-02 (CuMax):
            // Al ingresar nueva mercadería al almacén, si stock_actual + ingreso > CuMax, lanzar un MessageBox:
            // "Límite físico de almacenamiento superado"
            if (!_inventoryService.RegistrarIngresoMercaderia(ProductoSeleccionado.Id, cantidad, out string mensajeError))
            {
                MessageBox.Show(mensajeError, "Alerta de Capacidad de Almacén", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Ingreso registrado con éxito.\nProducto: {ProductoSeleccionado.Nombre}\nCantidad ingresada: +{cantidad} {ProductoSeleccionado.UnidadMedida}", "Operación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);

            CantidadIngresoTexto = string.Empty;
            CargarInventario();
        }
    }
}
