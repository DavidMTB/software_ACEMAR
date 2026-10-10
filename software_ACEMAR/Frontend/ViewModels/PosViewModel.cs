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
        private readonly IPdfService _pdfService;

        private string _textoBusqueda = string.Empty;
        private Producto? _productoSeleccionado;
        private string _cantidadTexto = "1";
        private decimal _totalVenta;
        private decimal _subtotalVenta;
        private decimal _igvVenta;

        private string _unidadMedidaSeleccionada = "Unidad";

        // Venta Efectivo vs Crédito
        private string _tipoVentaSeleccionado = "Efectivo"; // "Efectivo" o "Credito"
        private ClienteCredito? _clienteCreditoSeleccionado;
        private string _nombreClienteProforma = "Cliente Mostrador";

        public ObservableCollection<Producto> ProductosFiltrados { get; } = new();
        public ObservableCollection<DetalleVenta> TicketActual { get; } = new();
        public ObservableCollection<ClienteCredito> ClientesCredito { get; } = new();
        public ObservableCollection<string> UnidadesDisponiblesProducto { get; } = new();

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
            IProformaService proformaService,
            IPdfService pdfService)
        {
            _inventoryService = inventoryService;
            _salesService = salesService;
            _authService = authService;
            _creditService = creditService;
            _proformaService = proformaService;
            _pdfService = pdfService;

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

        private decimal _precioUnitarioSeleccionado;

        public Producto? ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set
            {
                if (SetField(ref _productoSeleccionado, value))
                {
                    ActualizarUnidadesDisponibles(value);
                    ActualizarPrecioUnitarioSeleccionado();
                    if (value != null && (string.IsNullOrWhiteSpace(CantidadTexto) || CantidadTexto == "0"))
                    {
                        CantidadTexto = "1";
                    }
                }
            }
        }

        public string UnidadMedidaSeleccionada
        {
            get => _unidadMedidaSeleccionada;
            set
            {
                if (SetField(ref _unidadMedidaSeleccionada, value))
                {
                    ActualizarPrecioUnitarioSeleccionado();
                }
            }
        }

        public decimal PrecioUnitarioSeleccionado
        {
            get => _precioUnitarioSeleccionado;
            set => SetField(ref _precioUnitarioSeleccionado, value);
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

        private void ActualizarUnidadesDisponibles(Producto? prod)
        {
            UnidadesDisponiblesProducto.Clear();
            if (prod == null) return;

            var unidades = prod.ObtenerUnidadesDisponibles();
            foreach (var u in unidades)
            {
                if (!UnidadesDisponiblesProducto.Contains(u, StringComparer.OrdinalIgnoreCase))
                {
                    UnidadesDisponiblesProducto.Add(u);
                }
            }

            var dictPrecios = prod.ObtenerPreciosPorUnidad();
            foreach (var k in dictPrecios.Keys)
            {
                if (!UnidadesDisponiblesProducto.Contains(k, StringComparer.OrdinalIgnoreCase))
                {
                    UnidadesDisponiblesProducto.Add(k);
                }
            }

            if (!string.IsNullOrWhiteSpace(prod.UnidadMedida) && !UnidadesDisponiblesProducto.Contains(prod.UnidadMedida, StringComparer.OrdinalIgnoreCase))
            {
                UnidadesDisponiblesProducto.Insert(0, prod.UnidadMedida);
            }

            UnidadMedidaSeleccionada = UnidadesDisponiblesProducto.FirstOrDefault() ?? (string.IsNullOrWhiteSpace(prod.UnidadMedida) ? "Unidad" : prod.UnidadMedida);
        }

        private void ActualizarPrecioUnitarioSeleccionado()
        {
            if (ProductoSeleccionado != null)
            {
                string unidad = string.IsNullOrWhiteSpace(UnidadMedidaSeleccionada) ? ProductoSeleccionado.UnidadMedida : UnidadMedidaSeleccionada;
                decimal precioFifo = _salesService.ObtenerPrecioVigente(ProductoSeleccionado.Id);
                
                if (precioFifo > 0 && string.Equals(unidad, ProductoSeleccionado.UnidadMedida, StringComparison.OrdinalIgnoreCase))
                {
                    PrecioUnitarioSeleccionado = precioFifo;
                }
                else
                {
                    PrecioUnitarioSeleccionado = ProductoSeleccionado.ObtenerPrecioParaUnidad(unidad);
                }
            }
            else
            {
                PrecioUnitarioSeleccionado = 0m;
            }
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

            string unidad = string.IsNullOrWhiteSpace(UnidadMedidaSeleccionada) ? ProductoSeleccionado.UnidadMedida : UnidadMedidaSeleccionada;
            decimal factor = ProductoSeleccionado.ObtenerFactorParaUnidad(unidad);

            var itemExistente = TicketActual.FirstOrDefault(t => t.ProductoId == ProductoSeleccionado.Id);

            decimal cantBaseExistente = 0m;
            foreach (var itemTicket in TicketActual.Where(t => t.ProductoId == ProductoSeleccionado.Id))
            {
                decimal itemFactor = ProductoSeleccionado.ObtenerFactorParaUnidad(itemTicket.UnidadMedida);
                cantBaseExistente += (itemTicket.Cantidad * itemFactor);
            }

            decimal cantBaseNueva = cantidad * factor;
            decimal cantBaseTotalSolicitada = cantBaseExistente + cantBaseNueva;

            // Regla INV-01 (Bloqueo de Venta por Stock Insuficiente)
            if (cantBaseTotalSolicitada > ProductoSeleccionado.StockActual)
            {
                MessageBox.Show($"Stock insuficiente. El producto '{ProductoSeleccionado.Nombre}' cuenta con {ProductoSeleccionado.StockActual:0.##} {ProductoSeleccionado.UnidadMedida} en stock (se requieren {cantBaseTotalSolicitada:0.##} {ProductoSeleccionado.UnidadMedida} para {cantidad:0.##} {unidad}).", "Validación de Inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Control de Regla de Stock Mínimo / Punto de Reorden
            decimal stockRestante = ProductoSeleccionado.StockActual - cantBaseTotalSolicitada;
            if (ProductoSeleccionado.StockMinimo > 0 && stockRestante < ProductoSeleccionado.StockMinimo)
            {
                var confirmacion = MessageBox.Show(
                    $"⚠️ ALERTA DE CONTROL DE STOCK MÍNIMO:\n\n" +
                    $"El producto '{ProductoSeleccionado.Nombre}' tiene un Stock Mínimo de seguridad configurado en {ProductoSeleccionado.StockMinimo:0.##} {ProductoSeleccionado.UnidadMedida}.\n\n" +
                    $"• Stock Actual: {ProductoSeleccionado.StockActual:0.##} {ProductoSeleccionado.UnidadMedida}\n" +
                    $"• Requerido en Stock Base: {cantBaseTotalSolicitada:0.##} {ProductoSeleccionado.UnidadMedida} ({cantidad:0.##} {unidad})\n" +
                    $"• Stock Restante: {stockRestante:0.##} {ProductoSeleccionado.UnidadMedida}\n\n" +
                    $"La venta dejará el stock por debajo del límite de seguridad de almacén. ¿Desea autorizar y agregar al ticket de todos modos?",
                    "Control de Stock Mínimo - ACEMAR",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmacion != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            decimal precioUnitarioFinal = ProductoSeleccionado.ObtenerPrecioParaUnidad(unidad);

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidad;
                itemExistente.UnidadMedida = unidad;
                itemExistente.PrecioUnitario = precioUnitarioFinal;
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
                    PrecioUnitario = precioUnitarioFinal,
                    UnidadMedida = unidad
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

            // Validar stock disponible y alerta de stock mínimo para todos los ítems antes de procesar
            foreach (var item in TicketActual)
            {
                var prod = _inventoryService.ObtenerPorId(item.ProductoId);
                if (prod != null)
                {
                    decimal factorItem = prod.ObtenerFactorParaUnidad(item.UnidadMedida);
                    decimal cantBaseRequerida = item.Cantidad * factorItem;

                    if (cantBaseRequerida > prod.StockActual)
                    {
                        MessageBox.Show($"Stock insuficiente para '{prod.Nombre}'. Disponible: {prod.StockActual:0.##} {prod.UnidadMedida} (se requieren {cantBaseRequerida:0.##} {prod.UnidadMedida} para {item.Cantidad:0.##} {item.UnidadMedida}).", "Error al Procesar Venta", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    decimal restante = prod.StockActual - cantBaseRequerida;
                    if (prod.StockMinimo > 0 && restante < prod.StockMinimo)
                    {
                        var resMin = MessageBox.Show(
                            $"⚠️ CONTROL DE STOCK MÍNIMO DE ALMACÉN:\n\n" +
                            $"El producto '{prod.Nombre}' tiene un Stock Mínimo de {prod.StockMinimo:0.##} {prod.UnidadMedida}.\n" +
                            $"Al completar esta venta quedará en {restante:0.##} {prod.UnidadMedida}.\n\n" +
                            $"¿Desea autorizar el despacho y completar la venta?",
                            "Control de Stock Mínimo - ACEMAR",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (resMin != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                }
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

            string tipoTexto = TipoVentaSeleccionado switch
            {
                "Credito" => $"A CRÉDITO (Fiado para {ClienteCreditoSeleccionado?.Nombre})",
                "Digital" => "AL CONTADO (Yape / Plin / Transferencia)",
                "Yape" => "AL CONTADO (Yape)",
                "Plin" => "AL CONTADO (Plin)",
                "Transferencia" => "AL CONTADO (Transferencia)",
                _ => "AL CONTADO / EFECTIVO"
            };
            var confirmacion = MessageBox.Show($"¿Confirmar venta por un total de S/ {TotalVenta:N2}?\nModalidad: {tipoTexto}", "Confirmación de Cobro", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmacion != MessageBoxResult.Yes) return;

            var venta = new Venta
            {
                Fecha = DateTime.Now,
                Total = TotalVenta,
                TipoVenta = EsVentaCredito ? "Credito" : (string.IsNullOrWhiteSpace(TipoVentaSeleccionado) ? "Efectivo" : TipoVentaSeleccionado),
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

            // ── Generar y guardar la Boleta en PDF en segundo plano (sin abrir visor automáticamente) ──
            try
            {
                byte[] pdfBytes = _pdfService.GenerarPdfBoletaVenta(venta);
                _pdfService.GuardarPdf(pdfBytes, "Ventas", $"{venta.NumeroTicket}.pdf");
            }
            catch (Exception exPdf)
            {
                // Silencioso o log interno para no interrumpir el flujo del cajero
                System.Diagnostics.Debug.WriteLine($"Error al guardar PDF en disco: {exPdf.Message}");
            }

            MessageBox.Show($"¡Venta procesada con éxito!\n\nTicket: {venta.NumeroTicket}\nModalidad: {tipoTexto}\nTotal: S/ {venta.Total:N2}\n\n(El comprobante PDF quedó registrado en la carpeta 'Boletas' y puede visualizarse o reimprimirse en 'Histórico de Ventas').", "Venta Finalizada", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    PrecioUnitario = d.PrecioUnitario,
                    UnidadMedida = d.UnidadMedida
                }).ToList()
            };

            if (!_proformaService.CrearProforma(proforma, out string error))
            {
                MessageBox.Show(error, "Error al Generar Proforma", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ── Generar y guardar PDF de Proforma ─────────────────────────────────
            try
            {
                byte[] pdfBytes = _pdfService.GenerarPdfProforma(proforma);
                _pdfService.GuardarPdf(pdfBytes, "Proformas", $"Proforma_{proforma.NumeroProforma}.pdf");
            }
            catch (Exception exPdf)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar PDF Proforma: {exPdf.Message}");
            }

            MessageBox.Show($"¡Proforma generada correctamente!\n\nCódigo: {proforma.NumeroProforma}\nCliente: {proforma.ClienteNombre}\nTotal Cotizado: S/ {proforma.Total:N2}\n\n(La cotización en PDF quedó guardada y puede consultarse en la sección 'Proformas').", "Proforma Guardada", MessageBoxButton.OK, MessageBoxImage.Information);

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
