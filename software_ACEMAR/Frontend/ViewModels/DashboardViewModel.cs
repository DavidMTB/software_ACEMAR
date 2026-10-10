using System;
using System.Collections.Generic;
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
        private readonly ISalesService _salesService;

        public event Action<string>? OnNavegarSolicitado;

        private Producto? _productoSeleccionado;
        private string _cantidadIngresoTexto = string.Empty;
        private string _filtroTexto = string.Empty;

        // KPIs Inventario
        private int _totalProductos;
        private int _totalProductosBajoStock;
        private decimal _valorTotalInventario;
        private decimal _promedioOcupacionCuMax;

        // KPIs Ventas
        private decimal _ventasDelDiaTotal;
        private decimal _ventasEfectivoTotal;
        private decimal _ventasCreditoTotal;
        private int _totalTransaccionesHoy;

        public ObservableCollection<Producto> Inventario { get; } = new();
        public ObservableCollection<Producto> ProductosBajoStockLista { get; } = new();
        public ObservableCollection<Venta> VentasRecientes { get; } = new();

        public ICommand CargarInventarioCommand { get; }
        public ICommand RegistrarIngresoCommand { get; }
        public ICommand LimpiarFiltroCommand { get; }
        public ICommand NavegarALotesCommand { get; }

        public DashboardViewModel(IInventoryService inventoryService, ISalesService salesService)
        {
            _inventoryService = inventoryService;
            _salesService = salesService;

            CargarInventarioCommand = new RelayCommand(CargarInventario);
            RegistrarIngresoCommand = new RelayCommand(EjecutarIngresoMercaderia);
            LimpiarFiltroCommand = new RelayCommand(() => FiltroTexto = string.Empty);
            NavegarALotesCommand = new RelayCommand(() => OnNavegarSolicitado?.Invoke("Lotes"));

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

        public decimal VentasDelDiaTotal
        {
            get => _ventasDelDiaTotal;
            set => SetField(ref _ventasDelDiaTotal, value);
        }

        public decimal VentasEfectivoTotal
        {
            get => _ventasEfectivoTotal;
            set => SetField(ref _ventasEfectivoTotal, value);
        }

        public decimal VentasCreditoTotal
        {
            get => _ventasCreditoTotal;
            set => SetField(ref _ventasCreditoTotal, value);
        }

        public int TotalTransaccionesHoy
        {
            get => _totalTransaccionesHoy;
            set => SetField(ref _totalTransaccionesHoy, value);
        }

        public void CargarInventario()
        {
            var todos = _inventoryService.ObtenerTodos();

            Inventario.Clear();
            ProductosBajoStockLista.Clear();
            foreach (var p in todos)
            {
                Inventario.Add(p);
                if (p.EsStockBajo)
                {
                    ProductosBajoStockLista.Add(p);
                }
            }

            var ventasHoy = _salesService.ObtenerVentasPorFecha(DateTime.Today, DateTime.Today);
            VentasRecientes.Clear();
            foreach (var v in ventasHoy.Take(10))
            {
                VentasRecientes.Add(v);
            }

            VentasDelDiaTotal = Math.Round(ventasHoy.Sum(v => v.Total), 2);
            VentasEfectivoTotal = Math.Round(ventasHoy.Where(v => v.TipoVenta == "Efectivo").Sum(v => v.Total), 2);
            VentasCreditoTotal = Math.Round(ventasHoy.Where(v => v.TipoVenta == "Credito").Sum(v => v.Total), 2);
            TotalTransaccionesHoy = ventasHoy.Count;

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

        private void ActualizarKpis(List<Producto> productos)
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
            MessageBox.Show("Todo ingreso de stock debe realizarse registrando un nuevo lote en la pestaña 'Lotes & Vencimientos' para garantizar el control FIFO y contabilidad de costos.", "Ingreso por Lotes Requerido", MessageBoxButton.OK, MessageBoxImage.Information);
            OnNavegarSolicitado?.Invoke("Lotes");
        }
    }
}
