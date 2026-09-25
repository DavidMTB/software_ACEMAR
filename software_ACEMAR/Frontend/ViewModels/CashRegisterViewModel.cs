using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class CashRegisterViewModel : ViewModelBase
    {
        private readonly ICashRegisterService _cashRegisterService;
        private readonly IAuthService _authService;

        private DateTime _fechaSeleccionada = DateTime.Today;
        private decimal _montoApertura = 150.00m;
        private decimal _totalVentasEfectivo;
        private decimal _totalVentasCredito;
        private decimal _totalAbonosEfectivo;
        private decimal _totalEfectivoEnCaja;
        private string _observacionesCierre = string.Empty;

        public ObservableCollection<MovimientoCaja> Movimientos { get; } = new();

        public ICommand CalcularArqueoCommand { get; }
        public ICommand GuardarArqueoCommand { get; }

        public CashRegisterViewModel(ICashRegisterService cashRegisterService, IAuthService authService)
        {
            _cashRegisterService = cashRegisterService;
            _authService = authService;

            CalcularArqueoCommand = new RelayCommand(ActualizarBalance);
            GuardarArqueoCommand = new RelayCommand(EjecutarGuardarArqueo);

            ActualizarBalance();
        }

        public DateTime FechaSeleccionada
        {
            get => _fechaSeleccionada;
            set
            {
                if (SetField(ref _fechaSeleccionada, value))
                {
                    ActualizarBalance();
                }
            }
        }

        public decimal MontoApertura
        {
            get => _montoApertura;
            set
            {
                if (SetField(ref _montoApertura, value))
                {
                    TotalEfectivoEnCaja = MontoApertura + TotalVentasEfectivo + TotalAbonosEfectivo;
                }
            }
        }

        public decimal TotalVentasEfectivo { get => _totalVentasEfectivo; set => SetField(ref _totalVentasEfectivo, value); }
        public decimal TotalVentasCredito { get => _totalVentasCredito; set => SetField(ref _totalVentasCredito, value); }
        public decimal TotalAbonosEfectivo { get => _totalAbonosEfectivo; set => SetField(ref _totalAbonosEfectivo, value); }
        public decimal TotalEfectivoEnCaja { get => _totalEfectivoEnCaja; set => SetField(ref _totalEfectivoEnCaja, value); }
        public string ObservacionesCierre { get => _observacionesCierre; set => SetField(ref _observacionesCierre, value); }

        public void ActualizarBalance()
        {
            var resumen = _cashRegisterService.ObtenerResumenDelDia(FechaSeleccionada, MontoApertura);

            TotalVentasEfectivo = resumen.TotalVentasEfectivo;
            TotalVentasCredito = resumen.TotalVentasCredito;
            TotalAbonosEfectivo = resumen.TotalAbonosEfectivo;
            TotalEfectivoEnCaja = resumen.TotalEfectivoCaja;

            Movimientos.Clear();
            foreach (var m in resumen.Movimientos)
            {
                Movimientos.Add(m);
            }
        }

        private void EjecutarGuardarArqueo()
        {
            var confirm = MessageBox.Show($"¿Desea registrar el Arqueo y Cierre de Caja para la fecha {FechaSeleccionada:dd/MM/yyyy}?\nTotal en Efectivo: S/ {TotalEfectivoEnCaja:N2}\nTotal Fiados: S/ {TotalVentasCredito:N2}", "Confirmar Cierre de Caja", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var arqueo = new ArqueoCaja
            {
                Fecha = FechaSeleccionada,
                MontoApertura = MontoApertura,
                TotalVentasEfectivo = TotalVentasEfectivo,
                TotalVentasCredito = TotalVentasCredito,
                TotalAbonosEfectivo = TotalAbonosEfectivo,
                Estado = "Cerrada",
                Observaciones = string.IsNullOrWhiteSpace(ObservacionesCierre) ? "Cierre de jornada en mostrador" : ObservacionesCierre.Trim(),
                UsuarioResponsable = _authService.UsuarioActual.NombreCompleto
            };

            if (!_cashRegisterService.GuardarArqueoCaja(arqueo, out string error))
            {
                MessageBox.Show(error, "Error en Arqueo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Arqueo de Caja guardado con éxito en la base de datos local!\nTotal Efectivo: S/ {TotalEfectivoEnCaja:N2}", "Arqueo Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            ObservacionesCierre = string.Empty;
        }
    }
}
