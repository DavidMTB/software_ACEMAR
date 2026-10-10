using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class CashRegisterViewModel : ViewModelBase
    {
        private readonly ICashRegisterService _cashRegisterService;
        private readonly IAuthService _authService;
        private readonly IExcelExportService _excelExportService;

        private string _tipoPeriodoSeleccionado = "Diario"; // "Diario", "Semanal", "Mensual", "Personalizado"
        private DateTime _fechaDesde = DateTime.Today;
        private DateTime _fechaHasta = DateTime.Today;
        private decimal _montoApertura = 150.00m;

        // Métricas de Balance y Contabilidad
        private decimal _totalVentasEfectivo;
        private decimal _totalVentasDigital;
        private decimal _totalVentasCredito;
        private decimal _totalAbonosEfectivo;
        private decimal _totalGastosLotes;
        private decimal _totalCostoVentas;
        private decimal _utilidadBruta;
        private decimal _totalEfectivoEnCaja;
        private string _observacionesCierre = string.Empty;

        public ObservableCollection<MovimientoCaja> Movimientos { get; } = new();

        public ICommand CalcularArqueoCommand { get; }
        public ICommand GuardarArqueoCommand { get; }
        public ICommand SeleccionarPeriodoCommand { get; }
        public ICommand ExportarExcelCommand { get; }

        public CashRegisterViewModel(ICashRegisterService cashRegisterService, IAuthService authService, IExcelExportService? excelExportService = null)
        {
            _cashRegisterService = cashRegisterService;
            _authService = authService;
            _excelExportService = excelExportService ?? new ExcelExportService();

            CalcularArqueoCommand = new RelayCommand(ActualizarBalance);
            GuardarArqueoCommand = new RelayCommand(EjecutarGuardarArqueo);
            SeleccionarPeriodoCommand = new RelayCommand(p => EstablecerPeriodo(p?.ToString() ?? "Diario"));
            ExportarExcelCommand = new RelayCommand(EjecutarExportarExcel);

            EstablecerPeriodo("Diario");
        }

        public string TipoPeriodoSeleccionado
        {
            get => _tipoPeriodoSeleccionado;
            set => SetField(ref _tipoPeriodoSeleccionado, value);
        }

        public DateTime FechaDesde
        {
            get => _fechaDesde;
            set
            {
                if (SetField(ref _fechaDesde, value))
                {
                    if (TipoPeriodoSeleccionado != "Personalizado")
                    {
                        TipoPeriodoSeleccionado = "Personalizado";
                    }
                    ActualizarBalance();
                }
            }
        }

        public DateTime FechaHasta
        {
            get => _fechaHasta;
            set
            {
                if (SetField(ref _fechaHasta, value))
                {
                    if (TipoPeriodoSeleccionado != "Personalizado")
                    {
                        TipoPeriodoSeleccionado = "Personalizado";
                    }
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
                    ActualizarBalance();
                }
            }
        }

        public decimal TotalVentasEfectivo { get => _totalVentasEfectivo; set => SetField(ref _totalVentasEfectivo, value); }
        public decimal TotalVentasDigital { get => _totalVentasDigital; set => SetField(ref _totalVentasDigital, value); }
        public decimal TotalVentasCredito { get => _totalVentasCredito; set => SetField(ref _totalVentasCredito, value); }
        public decimal TotalAbonosEfectivo { get => _totalAbonosEfectivo; set => SetField(ref _totalAbonosEfectivo, value); }
        public decimal TotalGastosLotes { get => _totalGastosLotes; set => SetField(ref _totalGastosLotes, value); }
        public decimal TotalCostoVentas { get => _totalCostoVentas; set => SetField(ref _totalCostoVentas, value); }
        public decimal UtilidadBruta { get => _utilidadBruta; set => SetField(ref _utilidadBruta, value); }
        public decimal TotalEfectivoEnCaja { get => _totalEfectivoEnCaja; set => SetField(ref _totalEfectivoEnCaja, value); }
        public string ObservacionesCierre { get => _observacionesCierre; set => SetField(ref _observacionesCierre, value); }

        public void EstablecerPeriodo(string periodo)
        {
            _tipoPeriodoSeleccionado = periodo;
            OnPropertyChanged(nameof(TipoPeriodoSeleccionado));

            var hoy = DateTime.Today;
            switch (periodo)
            {
                case "Diario":
                    _fechaDesde = hoy;
                    _fechaHasta = hoy;
                    break;
                case "Semanal":
                    // Últimos 7 días
                    _fechaDesde = hoy.AddDays(-6);
                    _fechaHasta = hoy;
                    break;
                case "Mensual":
                    // Primer día del mes hasta hoy
                    _fechaDesde = new DateTime(hoy.Year, hoy.Month, 1);
                    _fechaHasta = hoy;
                    break;
            }

            OnPropertyChanged(nameof(FechaDesde));
            OnPropertyChanged(nameof(FechaHasta));

            ActualizarBalance();
        }

        public void ActualizarBalance()
        {
            var resumen = _cashRegisterService.ObtenerResumenPorRango(FechaDesde, FechaHasta, MontoApertura);

            TotalVentasEfectivo = resumen.TotalVentasEfectivo;
            TotalVentasDigital = resumen.TotalVentasDigital;
            TotalVentasCredito = resumen.TotalVentasCredito;
            TotalAbonosEfectivo = resumen.TotalAbonosEfectivo;
            TotalGastosLotes = resumen.TotalGastosLotes;
            TotalCostoVentas = resumen.TotalCostoVentas;
            UtilidadBruta = resumen.UtilidadBruta;
            TotalEfectivoEnCaja = resumen.TotalEfectivoCaja;

            Movimientos.Clear();
            if (resumen.Movimientos != null)
            {
                foreach (var m in resumen.Movimientos)
                {
                    Movimientos.Add(m);
                }
            }
        }

        private void EjecutarGuardarArqueo()
        {
            var confirm = MessageBox.Show($"¿Desea registrar el Arqueo y Cierre de Caja para la fecha {FechaHasta:dd/MM/yyyy}?\nTotal en Efectivo: S/ {TotalEfectivoEnCaja:N2}\nTotal Fiados: S/ {TotalVentasCredito:N2}", "Confirmar Cierre de Caja", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var arqueo = new ArqueoCaja
            {
                Fecha = FechaHasta,
                MontoApertura = MontoApertura,
                TotalVentasEfectivo = TotalVentasEfectivo,
                TotalVentasCredito = TotalVentasCredito,
                TotalAbonosEfectivo = TotalAbonosEfectivo,
                Estado = "Cerrada",
                Observaciones = string.IsNullOrWhiteSpace(ObservacionesCierre) ? $"Cierre de caja ({TipoPeriodoSeleccionado})" : ObservacionesCierre.Trim(),
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

        private void EjecutarExportarExcel()
        {
            string periodoLabel = TipoPeriodoSeleccionado switch
            {
                "Diario" => $"Diario ({FechaDesde:dd/MM/yyyy})",
                "Semanal" => $"Semanal ({FechaDesde:dd/MM/yyyy} al {FechaHasta:dd/MM/yyyy})",
                "Mensual" => $"Mensual ({FechaDesde:MMMM yyyy})",
                _ => $"Personalizado ({FechaDesde:dd/MM/yyyy} al {FechaHasta:dd/MM/yyyy})"
            };

            var dialog = new SaveFileDialog
            {
                Title = "Exportar Cierre y Arqueo de Caja a Hoja de Cálculo (Excel)",
                Filter = "Libro de Excel (*.xlsx)|*.xlsx|Archivo CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                FileName = $"Reporte_Contable_ACEMAR_{FechaDesde:yyyyMMdd}_al_{FechaHasta:yyyyMMdd}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                var resumen = _cashRegisterService.ObtenerResumenPorRango(FechaDesde, FechaHasta, MontoApertura);

                if (_excelExportService.ExportarResumenCaja(resumen, periodoLabel, _authService.UsuarioActual.NombreCompleto, dialog.FileName, out string error))
                {
                    var result = MessageBox.Show($"¡Reporte contable de caja exportado con éxito a Excel en formato multipestaña profesional!\n\nRuta: {dialog.FileName}\n\n¿Desea abrir el archivo en Excel ahora mismo?",
                                                 "Exportación Exitosa ACEMAR", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dialog.FileName,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
                else
                {
                    MessageBox.Show(error, "Error en Exportación", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
