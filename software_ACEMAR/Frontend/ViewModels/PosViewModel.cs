using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class PosViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ISalesService _salesService;
        private readonly IAuthService _authService;
        private readonly ICreditService _creditService;
        private readonly IProformaService _proformaService;

        private string _textoBusqueda = string.Empty;
        private Producto? _productoSeleccionado;
        private string _cantidadTexto = "1";
        private decimal _totalVenta;
        private decimal _subtotalVenta;
        private decimal _igvVenta;

        // Venta Efectivo vs Crédito
        private string _tipoVentaSeleccionado = "Efectivo"; // "Efectivo" o "Credito"
        private ClienteCredito? _clienteCreditoSeleccionado;
        private string _nombreClienteProforma = "Cliente Mostrador";

        public ObservableCollection<Producto> ProductosFiltrados { get; } = new();
        public ObservableCollection<DetalleVenta> TicketActual { get; } = new();
        public ObservableCollection<ClienteCredito> ClientesCredito { get; } = new();

        public ICommand BuscarCommand { get; }
        public ICommand LimpiarBusquedaCommand { get; }
        public ICommand AgregarAlTicketCommand { get; }
        public ICommand EliminarDelTicketCommand { get; }
        public ICommand ProcesarVentaCommand { get; }
        public ICommand CancelarTicketCommand { get; }
        public ICommand GenerarProformaCommand { get; }

        public PosViewModel(
            IInventoryService inventoryService, 
            ISalesService salesService, 
            IAuthService authService,
            ICreditService creditService,
            IProformaService proformaService)
        {
            _inventoryService = inventoryService;
            _salesService = salesService;
            _authService = authService;
            _creditService = creditService;
            _proformaService = proformaService;

            BuscarCommand = new RelayCommand(EjecutarBusqueda);
            LimpiarBusquedaCommand = new RelayCommand(LimpiarBusqueda);
            AgregarAlTicketCommand = new RelayCommand(EjecutarAgregarAlTicket);
            EliminarDelTicketCommand = new RelayCommand(EjecutarEliminarItem);
            ProcesarVentaCommand = new RelayCommand(EjecutarProcesarVenta, () => TicketActual.Any());
            CancelarTicketCommand = new RelayCommand(LimpiarTicket, () => TicketActual.Any());
            GenerarProformaCommand = new RelayCommand(EjecutarGenerarProforma, () => TicketActual.Any());

            CargarProductos();
            CargarClientesCredito();
        }

        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                if (SetField(ref _textoBusqueda, value))
                {
                    EjecutarBusqueda();
                }
            }
        }

        public Producto? ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set
            {
                if (SetField(ref _productoSeleccionado, value))
                {
                    if (value != null && (string.IsNullOrWhiteSpace(CantidadTexto) || CantidadTexto == "0"))
                    {
                        CantidadTexto = "1";
                    }
                }
            }
        }

        public string CantidadTexto
        {
            get => _cantidadTexto;
            set => SetField(ref _cantidadTexto, value);
        }

        public decimal TotalVenta
        {
            get => _totalVenta;
            set => SetField(ref _totalVenta, value);
        }

        public decimal SubtotalVenta
        {
            get => _subtotalVenta;
            set => SetField(ref _subtotalVenta, value);
        }

        public decimal IgvVenta
        {
            get => _igvVenta;
            set => SetField(ref _igvVenta, value);
        }

        public string TipoVentaSeleccionado
        {
            get => _tipoVentaSeleccionado;
            set
            {
                if (SetField(ref _tipoVentaSeleccionado, value))
                {
                    OnPropertyChanged(nameof(EsVentaCredito));
                }
            }
        }

        public bool EsVentaCredito => TipoVentaSeleccionado == "Credito";

        public ClienteCredito? ClienteCreditoSeleccionado
        {
            get => _clienteCreditoSeleccionado;
            set => SetField(ref _clienteCreditoSeleccionado, value);
        }

        public string NombreClienteProforma
        {
            get => _nombreClienteProforma;
            set => SetField(ref _nombreClienteProforma, value);
        }

        public void CargarProductos()
        {
            var lista = _inventoryService.BuscarPorNombre(TextoBusqueda);
            ProductosFiltrados.Clear();
            foreach (var p in lista)
            {
                ProductosFiltrados.Add(p);
            }
        }

        public void CargarClientesCredito()
        {
            var clientes = _creditService.ObtenerTodos();
            ClientesCredito.Clear();
            foreach (var c in clientes)
            {
                ClientesCredito.Add(c);
            }
            if (ClienteCreditoSeleccionado == null && ClientesCredito.Any())
            {
                ClienteCreditoSeleccionado = ClientesCredito.First();
            }
        }

        private void EjecutarBusqueda()
        {
            var resultados = _inventoryService.BuscarPorNombre(TextoBusqueda);
            ProductosFiltrados.Clear();
            foreach (var item in resultados)
            {
                ProductosFiltrados.Add(item);
            }
        }

        private void LimpiarBusqueda()
        {
            TextoBusqueda = string.Empty;
            EjecutarBusqueda();
        }

        private void EjecutarAgregarAlTicket()
        {
            if (ProductoSeleccionado == null)
            {
                MessageBox.Show("Por favor, seleccione un producto de la lista.", "Punto de Venta ACEMAR", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Regla POS-02 (Fracciones)
            if (!decimal.TryParse(CantidadTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cantidad)
                && !decimal.TryParse(CantidadTexto, out cantidad))
            {
                MessageBox.Show("Ingrese una cantidad numérica válida (admite decimales, ej: 1.5, 0.75).", "Error de Formato", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cantidad <= 0)
            {
                MessageBox.Show("La cantidad a vender debe ser mayor a cero.", "Validación de Cantidad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var itemExistente = TicketActual.FirstOrDefault(t => t.ProductoId == ProductoSeleccionado.Id);
            decimal cantidadTotalSolicitada = cantidad + (itemExistente?.Cantidad ?? 0m);

            // Regla INV-01 (Bloqueo de Venta)
            if (cantidadTotalSolicitada > ProductoSeleccionado.StockActual)
            {
                MessageBox.Show("Stock insuficiente", "Validación de Inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidad;
                int index = TicketActual.IndexOf(itemExistente);
                TicketActual.RemoveAt(index);
                TicketActual.Insert(index, itemExistente);
            }
            else
            {
                var nuevoDetalle = new DetalleVenta
                {
                    ProductoId = ProductoSeleccionado.Id,
                    Producto = ProductoSeleccionado,
                    ProductoNombre = ProductoSeleccionado.Nombre,
                    Cantidad = cantidad,
                    PrecioUnitario = ProductoSeleccionado.PrecioUnitario
                };
                TicketActual.Add(nuevoDetalle);
            }

            RecalcularTotales();
            CantidadTexto = "1";
        }

        private void EjecutarEliminarItem(object? param)
        {
            if (param is DetalleVenta detalle)
            {
                TicketActual.Remove(detalle);
                RecalcularTotales();
            }
        }

        private void RecalcularTotales()
        {
            decimal total = Math.Round(TicketActual.Sum(d => d.Subtotal), 2);
            TotalVenta = total;
            SubtotalVenta = Math.Round(total / 1.18m, 2);
            IgvVenta = Math.Round(total - SubtotalVenta, 2);
        }

        private void LimpiarTicket()
        {
            if (TicketActual.Any())
            {
                var result = MessageBox.Show("¿Está seguro de que desea cancelar y vaciar el ticket actual?", "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    TicketActual.Clear();
                    RecalcularTotales();
                }
            }
        }

        private void EjecutarProcesarVenta()
        {
            if (!TicketActual.Any())
            {
                MessageBox.Show("El ticket de venta está vacío.", "Punto de Venta ACEMAR", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Si es venta a crédito, validar cliente y su límite disponible
            if (EsVentaCredito)
            {
                if (ClienteCreditoSeleccionado == null)
                {
                    MessageBox.Show("Seleccione el cliente al cual se le asignará el fiado.", "Venta a Crédito", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Regla de seguridad de fiados CRE-01
                if (!_authService.PuedeAccederCreditos(out string errPermiso))
                {
                    MessageBox.Show(errPermiso, "Seguridad de Fiados", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                decimal nuevaDeuda = ClienteCreditoSeleccionado.DeudaActual + TotalVenta;
                if (nuevaDeuda > ClienteCreditoSeleccionado.LimiteCredito)
                {
                    MessageBox.Show($"Límite de crédito excedido para {ClienteCreditoSeleccionado.Nombre}.\nCrédito disponible: S/ {ClienteCreditoSeleccionado.CreditoDisponible:N2}\nTotal de la venta: S/ {TotalVenta:N2}", "Límite Excedido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string tipoTexto = EsVentaCredito ? $"A CRÉDITO (Fiado para {ClienteCreditoSeleccionado?.Nombre})" : "AL CONTADO / EFECTIVO";
            var confirmacion = MessageBox.Show($"¿Confirmar venta por un total de S/ {TotalVenta:N2}?\nModalidad: {tipoTexto}", "Confirmación de Cobro", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmacion != MessageBoxResult.Yes) return;

            var venta = new Venta
            {
                Fecha = DateTime.Now,
                Total = TotalVenta,
                TipoVenta = EsVentaCredito ? "Credito" : "Efectivo",
                ClienteCreditoId = EsVentaCredito ? ClienteCreditoSeleccionado?.Id : null,
                ClienteNombre = EsVentaCredito ? ClienteCreditoSeleccionado?.Nombre : "Cliente Mostrador",
                UsuarioVendedor = _authService.UsuarioActual.NombreCompleto,
                Detalles = TicketActual.ToList()
            };

            if (!_salesService.ProcesarVenta(venta, out string mensajeError))
            {
                MessageBox.Show(mensajeError, "Error al Procesar Venta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Si fue a crédito, actualizar la deuda del cliente en la BD
            if (EsVentaCredito && ClienteCreditoSeleccionado != null)
            {
                _creditService.RegistrarNuevoFiado(ClienteCreditoSeleccionado.Id, TotalVenta, $"Venta ticket {venta.NumeroTicket}", _authService.UsuarioActual.NombreCompleto, out _);
            }

            MessageBox.Show($"¡Venta procesada con éxito!\nTicket: {venta.NumeroTicket}\nModalidad: {tipoTexto}\nTotal: S/ {venta.Total:N2}", "Venta Finalizada", MessageBoxButton.OK, MessageBoxImage.Information);

            TicketActual.Clear();
            RecalcularTotales();
            CargarProductos();
            CargarClientesCredito();
        }

        private void EjecutarGenerarProforma()
        {
            if (!TicketActual.Any())
            {
                MessageBox.Show("Agregue productos al ticket para poder generar una proforma.", "Proforma / Cotización", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var proforma = new Proforma
            {
                ClienteNombre = string.IsNullOrWhiteSpace(NombreClienteProforma) ? "Cliente Mostrador" : NombreClienteProforma.Trim(),
                Total = TotalVenta,
                Usuario = _authService.UsuarioActual.NombreCompleto,
                Detalles = TicketActual.Select(d => new DetalleProforma
                {
                    ProductoId = d.ProductoId,
                    ProductoNombre = d.ProductoNombre,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                }).ToList()
            };

            if (!_proformaService.CrearProforma(proforma, out string error))
            {
                MessageBox.Show(error, "Error al Generar Proforma", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Proforma generada correctamente!\nCódigo: {proforma.NumeroProforma}\nCliente: {proforma.ClienteNombre}\nTotal Cotizado: S/ {proforma.Total:N2}\n(No se descuenta stock de almacén)", "Proforma Guardada", MessageBoxButton.OK, MessageBoxImage.Information);

            TicketActual.Clear();
            RecalcularTotales();
        }

        public void CargarItemsDesdeProforma(Proforma p)
        {
            TicketActual.Clear();
            foreach (var item in p.Detalles)
            {
                var prod = _inventoryService.ObtenerPorId(item.ProductoId);
                TicketActual.Add(new DetalleVenta
                {
                    ProductoId = item.ProductoId,
                    Producto = prod,
                    ProductoNombre = item.ProductoNombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario
                });
            }
            RecalcularTotales();
            NombreClienteProforma = p.ClienteNombre;
            MessageBox.Show($"Se han cargado los ítems de la proforma {p.NumeroProforma} en el ticket de venta actual para su cobro.", "Proforma Cargada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
