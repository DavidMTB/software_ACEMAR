using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class VentasCorreccionViewModel : ViewModelBase
    {
        private readonly IVentaCorrectionService _ventaCorrectionService;
        private readonly ISalesService _salesService;
        private readonly IPdfService _pdfService;

        private string _busquedaVenta = string.Empty;
        private Venta? _ventaSeleccionada;
        private DetalleVenta? _detalleSeleccionado;

        // Edición de ítem seleccionado
        private string _nuevaCantidadTexto = string.Empty;
        private string _nuevoPrecioTexto = string.Empty;
        private string _motivoAuditoria = string.Empty;
        private string _usuarioLogueado = "Administrador";

        public ObservableCollection<Venta> Ventas { get; } = new();
        public ObservableCollection<DetalleVenta> DetallesVenta { get; } = new();
        public ObservableCollection<VentaAuditoria> HistorialAuditoria { get; } = new();

        public ICommand BuscarVentasCommand { get; }
        public ICommand SeleccionarVentaCommand { get; }
        public ICommand GuardarCorreccionCommand { get; }
        public ICommand AnularVentaCompletaCommand { get; }
        public ICommand VerBoletaPdfCommand { get; }
        public ICommand VerTicketTermicoPdfCommand { get; }

        public VentasCorreccionViewModel(IVentaCorrectionService ventaCorrectionService, ISalesService salesService, IPdfService pdfService)
        {
            _ventaCorrectionService = ventaCorrectionService;
            _salesService = salesService;
            _pdfService = pdfService;

            BuscarVentasCommand = new RelayCommand(CargarVentas);
            SeleccionarVentaCommand = new RelayCommand(EjecutarSeleccionarVenta);
            GuardarCorreccionCommand = new RelayCommand(EjecutarGuardarCorreccion);
            AnularVentaCompletaCommand = new RelayCommand(EjecutarAnularVentaCompleta);
            VerBoletaPdfCommand = new RelayCommand(EjecutarVerBoletaPdf);
            VerTicketTermicoPdfCommand = new RelayCommand(EjecutarVerTicketTermicoPdf);

            CargarVentas();
        }

        public string BusquedaVenta
        {
            get => _busquedaVenta;
            set
            {
                if (SetField(ref _busquedaVenta, value))
                {
                    CargarVentas();
                }
            }
        }

        public Venta? VentaSeleccionada
        {
            get => _ventaSeleccionada;
            set
            {
                if (SetField(ref _ventaSeleccionada, value) && value != null)
                {
                    CargarDetallesYAuditoria(value);
                }
            }
        }

        public DetalleVenta? DetalleSeleccionado
        {
            get => _detalleSeleccionado;
            set
            {
                if (SetField(ref _detalleSeleccionado, value) && value != null)
                {
                    NuevaCantidadTexto = value.Cantidad.ToString("0.##");
                    NuevoPrecioTexto = value.PrecioUnitario.ToString("0.00");
                }
            }
        }

        public string NuevaCantidadTexto { get => _nuevaCantidadTexto; set => SetField(ref _nuevaCantidadTexto, value); }
        public string NuevoPrecioTexto { get => _nuevoPrecioTexto; set => SetField(ref _nuevoPrecioTexto, value); }
        public string MotivoAuditoria { get => _motivoAuditoria; set => SetField(ref _motivoAuditoria, value); }
        public string UsuarioLogueado { get => _usuarioLogueado; set => SetField(ref _usuarioLogueado, value); }

        public void CargarVentas()
        {
            Ventas.Clear();
            var lista = _salesService.ObtenerVentasRecientes(100);
            if (!string.IsNullOrWhiteSpace(BusquedaVenta))
            {
                string t = BusquedaVenta.Trim().ToLower();
                lista = lista.Where(v => (v.ClienteNombre ?? "").ToLower().Contains(t) || v.Id.ToString().Contains(t) || (v.TipoVenta ?? "").ToLower().Contains(t)).ToList();
            }

            foreach (var v in lista)
            {
                Ventas.Add(v);
            }
        }

        private void EjecutarSeleccionarVenta(object? param)
        {
            if (param is Venta v)
            {
                VentaSeleccionada = v;
            }
        }

        private void CargarDetallesYAuditoria(Venta v)
        {
            DetallesVenta.Clear();
            HistorialAuditoria.Clear();

            var ventaCompleta = _ventaCorrectionService.ObtenerVenta(v.Id);
            if (ventaCompleta != null)
            {
                _ventaSeleccionada = ventaCompleta;
                if (ventaCompleta.Detalles != null)
                {
                    foreach (var d in ventaCompleta.Detalles)
                    {
                        DetallesVenta.Add(d);
                    }
                }
            }

            var auditoria = _ventaCorrectionService.ObtenerHistorialAuditoria(v.Id);
            foreach (var a in auditoria)
            {
                HistorialAuditoria.Add(a);
            }
        }

        private void EjecutarGuardarCorreccion()
        {
            if (VentaSeleccionada == null || DetalleSeleccionado == null)
            {
                MessageBox.Show("Seleccione una venta y un detalle específico a corregir.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(MotivoAuditoria) || MotivoAuditoria.Trim().Length < 5)
            {
                MessageBox.Show("Debe especificar una justificación de auditoría válida (mínimo 5 caracteres) para modificar una venta ya procesada.", "Auditoría Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(NuevaCantidadTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cant) || cant < 0)
            {
                MessageBox.Show("Ingrese una cantidad válida igual o mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(NuevoPrecioTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precio) || precio < 0)
            {
                MessageBox.Show("Ingrese un precio unitario válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"¿Confirma la modificación del ítem '{DetalleSeleccionado.ProductoNombre}' de la Venta #{VentaSeleccionada.Id}?\n\nCantidad anterior: {DetalleSeleccionado.Cantidad:N2} -> Nueva: {cant:N2}\nPrecio anterior: S/ {DetalleSeleccionado.PrecioUnitario:N2} -> Nuevo: S/ {precio:N2}\n\nSe ajustarán stocks de lotes y deuda de crédito si corresponde.", "Confirmar Corrección de Venta", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var (exito, error) = _ventaCorrectionService.CorregirDetalleVenta(VentaSeleccionada.Id, DetalleSeleccionado.Id, cant, precio, MotivoAuditoria, UsuarioLogueado);
            if (!exito)
            {
                MessageBox.Show(error, "Error en Corrección de Venta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Venta #{VentaSeleccionada.Id} corregida correctamente! Se ha registrado el asiento de auditoría de seguridad.", "Corrección Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            MotivoAuditoria = string.Empty;
            CargarVentas();
            if (_ventaSeleccionada != null)
            {
                CargarDetallesYAuditoria(_ventaSeleccionada);
            }
        }

        private void EjecutarAnularVentaCompleta()
        {
            if (VentaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una venta a anular.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(MotivoAuditoria) || MotivoAuditoria.Trim().Length < 5)
            {
                MessageBox.Show("Ingrese una justificación obligatoria en el motivo de auditoría para anular la venta.", "Auditoría Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"⚠️ ¿CONFIRMA LA ANULACIÓN COMPLETA DE LA VENTA #{VentaSeleccionada.Id}?\n\nTodos los productos serán reingresados al stock de lotes y la deuda del cliente será revertida.", "Anulación Total de Venta", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var (exito, error) = _ventaCorrectionService.AnularVentaCompleta(VentaSeleccionada.Id, MotivoAuditoria, UsuarioLogueado);
            if (!exito)
            {
                MessageBox.Show(error, "Error al Anular Venta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Venta #{VentaSeleccionada.Id} anulada y stock devuelto exitosamente.", "Anulación Registrada", MessageBoxButton.OK, MessageBoxImage.Information);
            MotivoAuditoria = string.Empty;
            CargarVentas();
        }

        private void EjecutarVerBoletaPdf(object? param)
        {
            var venta = param as Venta ?? VentaSeleccionada;
            if (venta == null)
            {
                MessageBox.Show("Seleccione una venta para visualizar la Boleta PDF.", "Visualizar PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ventaCompleta = _ventaCorrectionService.ObtenerVenta(venta.Id) ?? venta;
            try
            {
                byte[] pdfBytes = _pdfService.GenerarPdfBoletaVenta(ventaCompleta);
                string path = _pdfService.GuardarYAbrirPdf(pdfBytes, "Ventas", $"{ventaCompleta.NumeroTicket}.pdf");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar la Boleta PDF: {ex.Message}", "Error PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EjecutarVerTicketTermicoPdf(object? param)
        {
            var venta = param as Venta ?? VentaSeleccionada;
            if (venta == null)
            {
                MessageBox.Show("Seleccione una venta para visualizar el Ticket Térmico PDF.", "Visualizar PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ventaCompleta = _ventaCorrectionService.ObtenerVenta(venta.Id) ?? venta;
            try
            {
                byte[] pdfBytes = _pdfService.GenerarPdfTicketTermico(ventaCompleta);
                string path = _pdfService.GuardarYAbrirPdf(pdfBytes, "Tickets", $"Ticket_{ventaCompleta.NumeroTicket}.pdf");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar el Ticket Térmico PDF: {ex.Message}", "Error PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
