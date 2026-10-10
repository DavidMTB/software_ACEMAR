using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class VentaDiaItem
    {
        public string Dia { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public double AlturaBarra { get; set; } // 0 - 130 px
        public string MontoFormateado => $"S/{Monto:N0}";
    }

    public class TopProductoItem
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal CantidadVendida { get; set; }
        public decimal MontoTotal { get; set; }
        public double PorcentajeBarra { get; set; } // 0 - 100
        public string Unidad { get; set; } = "UND";
    }

    public class MedioPagoItem
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public double Porcentaje { get; set; }
        public string ColorHex { get; set; } = "#4CAF50";
        public string Icono { get; set; } = "💵";
    }

    public class DashboardGeneralViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ISalesService _salesService;
        private readonly ICreditService _creditService;
        private readonly IProformaService _proformaService;
        private readonly ICashRegisterService _cashRegisterService;
        private readonly ILoteService _loteService;

        public event Action<string>? OnNavegarSolicitado;

        private decimal _totalVentasHoy;
        private decimal _totalVentasMes;
        private decimal _totalDeudaCreditos;
        private int _totalProductosBajoStock;
        private int _totalLotesPorVencer;
        private decimal _saldoCajaChica;
        private int _totalProformasActivas;
        private int _totalProductosCatalogo;

        private string _puntosCerruchoLineaString = "0,100 40,80 80,110 120,40 160,95 200,30 240,85 280,20 320,70 380,40";
        private string _puntosCerruchoRellenoString = "0,140 0,100 40,80 80,110 120,40 160,95 200,30 240,85 280,20 320,70 380,40 380,140";

        public ObservableCollection<VentaDiaItem> VentasUltimos7Dias { get; } = new();
        public ObservableCollection<TopProductoItem> TopProductosMasVendidos { get; } = new();
        public ObservableCollection<MedioPagoItem> MediosPagoDistribucion { get; } = new();

        public ICommand ActualizarDashboardCommand { get; }
        public ICommand NavegarACommand { get; }

        public DashboardGeneralViewModel(
            IInventoryService inventoryService,
            ISalesService salesService,
            ICreditService creditService,
            IProformaService proformaService,
            ICashRegisterService cashRegisterService,
            ILoteService loteService)
        {
            _inventoryService = inventoryService;
            _salesService = salesService;
            _creditService = creditService;
            _proformaService = proformaService;
            _cashRegisterService = cashRegisterService;
            _loteService = loteService;

            ActualizarDashboardCommand = new RelayCommand(() => CargarDatosDashboard());
            NavegarACommand = new RelayCommand(p => OnNavegarSolicitado?.Invoke(p?.ToString() ?? "POS"));

            CargarDatosDashboard();
        }

        public decimal TotalVentasHoy
        {
            get => _totalVentasHoy;
            set => SetField(ref _totalVentasHoy, value);
        }

        public decimal TotalVentasMes
        {
            get => _totalVentasMes;
            set => SetField(ref _totalVentasMes, value);
        }

        public decimal TotalDeudaCreditos
        {
            get => _totalDeudaCreditos;
            set => SetField(ref _totalDeudaCreditos, value);
        }

        public int TotalProductosBajoStock
        {
            get => _totalProductosBajoStock;
            set => SetField(ref _totalProductosBajoStock, value);
        }

        public int TotalLotesPorVencer
        {
            get => _totalLotesPorVencer;
            set => SetField(ref _totalLotesPorVencer, value);
        }

        public decimal SaldoCajaChica
        {
            get => _saldoCajaChica;
            set => SetField(ref _saldoCajaChica, value);
        }

        public int TotalProformasActivas
        {
            get => _totalProformasActivas;
            set => SetField(ref _totalProformasActivas, value);
        }

        public int TotalProductosCatalogo
        {
            get => _totalProductosCatalogo;
            set => SetField(ref _totalProductosCatalogo, value);
        }

        public string PuntosCerruchoLineaString
        {
            get => _puntosCerruchoLineaString;
            set => SetField(ref _puntosCerruchoLineaString, value);
        }

        public string PuntosCerruchoRellenoString
        {
            get => _puntosCerruchoRellenoString;
            set => SetField(ref _puntosCerruchoRellenoString, value);
        }

        public void CargarDatosDashboard()
        {
            try
            {
                // 1. Productos y Stock
                var productos = _inventoryService.ObtenerTodos() ?? new List<Producto>();
                TotalProductosCatalogo = productos.Count;
                TotalProductosBajoStock = productos.Count(p => p.EsStockBajo);

                // 2. Ventas del día y del mes
                var hoy = DateTime.Today;
                var primerDiaMes = new DateTime(hoy.Year, hoy.Month, 1);
                var ventasMes = _salesService.ObtenerVentasPorFecha(primerDiaMes, hoy) ?? new List<Venta>();
                var ventasHoy = ventasMes.Where(v => v.Fecha.Date == hoy).ToList();

                TotalVentasHoy = Math.Round(ventasHoy.Sum(v => v.Total), 2);
                TotalVentasMes = Math.Round(ventasMes.Sum(v => v.Total), 2);

                // 3. Créditos / Fiados pendientes
                var clientesCredito = _creditService.ObtenerTodos() ?? new List<ClienteCredito>();
                TotalDeudaCreditos = Math.Round(clientesCredito.Sum(c => c.DeudaActual), 2);

                // 4. Proformas activas
                var proformas = _proformaService.ObtenerTodas() ?? new List<Proforma>();
                TotalProformasActivas = proformas.Count(p => p.Estado == "Pendiente" || p.Estado == "Emitida");

                // 5. Arqueo / Saldo de caja
                var resumenCaja = _cashRegisterService.ObtenerResumenDelDia(hoy, 500m);
                SaldoCajaChica = resumenCaja != null ? Math.Round(resumenCaja.TotalEfectivoCaja, 2) : 500m;

                // 6. Lotes por vencer (calculado desde productos con stock bajo o lotes activos)
                int countLotesBajos = 0;
                foreach (var p in productos)
                {
                    var lotesP = _loteService.ObtenerLotesActivos(p.Id);
                    if (lotesP != null)
                    {
                        countLotesBajos += lotesP.Count(l => l.CantidadDisponible <= 5);
                    }
                }
                TotalLotesPorVencer = countLotesBajos;

                // 7. Gráfico de Barras Verticales: Ventas de los últimos 7 días
                CargarVentasUltimos7Dias(ventasMes);

                // 8. Gráfico de Barras Horizontales: Top Productos más vendidos
                CargarTopProductos(ventasMes, productos);

                // 9. Gráfico de Donut: Medios de pago
                CargarDistribucionMediosPago(ventasMes);

                // 10. Gráfico de Cerrucho (Picos y Valles)
                GenerarGraficoCerrucho(ventasMes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Advertencia al cargar los datos del Dashboard:\n{ex.Message}", 
                                "Carga de Datos Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CargarVentasUltimos7Dias(List<Venta> ventasMes)
        {
            VentasUltimos7Dias.Clear();
            var hoy = DateTime.Today;
            var ultimos7Dias = Enumerable.Range(0, 7)
                .Select(i => hoy.AddDays(-6 + i))
                .ToList();

            decimal maxMonto = 1;
            var resumenDias = new List<(string Dia, decimal Monto)>();

            foreach (var fecha in ultimos7Dias)
            {
                string nombreDia = fecha.ToString("ddd d", new CultureInfo("es-ES")).ToUpper();
                decimal totalDia = ventasMes.Where(v => v.Fecha.Date == fecha.Date).Sum(v => v.Total);
                resumenDias.Add((nombreDia, totalDia));
                if (totalDia > maxMonto) maxMonto = totalDia;
            }

            foreach (var d in resumenDias)
            {
                double altura = 0;
                if (maxMonto > 0)
                {
                    altura = (double)(d.Monto / maxMonto) * 110.0;
                }
                altura = SanitizarDouble(altura, 0, 130, 0);
                if (altura < 12 && d.Monto > 0) altura = 12;

                VentasUltimos7Dias.Add(new VentaDiaItem
                {
                    Dia = d.Dia,
                    Monto = d.Monto,
                    AlturaBarra = altura
                });
            }
        }

        private void CargarTopProductos(List<Venta> ventasMes, List<Producto> productos)
        {
            TopProductosMasVendidos.Clear();

            var conteoProductos = new Dictionary<string, (decimal Cantidad, decimal Monto, string Unidad)>();

            foreach (var v in ventasMes)
            {
                if (v.Detalles != null)
                {
                    foreach (var d in v.Detalles)
                    {
                        string key = d.ProductoNombre;
                        if (!conteoProductos.ContainsKey(key))
                        {
                            conteoProductos[key] = (0, 0, d.UnidadMedida);
                        }
                        var actual = conteoProductos[key];
                        conteoProductos[key] = (actual.Cantidad + d.Cantidad, actual.Monto + d.Subtotal, d.UnidadMedida);
                    }
                }
            }

            var topList = conteoProductos
                .OrderByDescending(kv => kv.Value.Monto)
                .Take(5)
                .ToList();

            if (!topList.Any())
            {
                foreach (var p in productos.Take(5))
                {
                    topList.Add(new KeyValuePair<string, (decimal Cantidad, decimal Monto, string Unidad)>(
                        p.Nombre, (p.StockActual > 0 ? p.StockActual : 10, p.PrecioUnitario * 10, p.UnidadMedida)));
                }
            }

            decimal maxMonto = topList.Any() ? topList.Max(t => t.Value.Monto) : 1;
            if (maxMonto <= 0) maxMonto = 1;

            foreach (var item in topList)
            {
                double porcentaje = (double)(item.Value.Monto / maxMonto) * 100.0;
                porcentaje = SanitizarDouble(porcentaje, 0, 100, 15);
                if (porcentaje < 15) porcentaje = 15;

                TopProductosMasVendidos.Add(new TopProductoItem
                {
                    Nombre = item.Key,
                    CantidadVendida = item.Value.Cantidad,
                    MontoTotal = Math.Round(item.Value.Monto, 2),
                    PorcentajeBarra = porcentaje,
                    Unidad = item.Value.Unidad
                });
            }
        }

        private void CargarDistribucionMediosPago(List<Venta> ventasMes)
        {
            MediosPagoDistribucion.Clear();

            decimal totalEfectivo = ventasMes.Where(v => v.TipoVenta == "Efectivo").Sum(v => v.Total);
            decimal totalDigital = ventasMes.Where(v => v.TipoVenta == "Digital" || v.TipoVenta == "Yape" || v.TipoVenta == "Plin" || v.TipoVenta == "Transferencia").Sum(v => v.Total);
            decimal totalCredito = ventasMes.Where(v => v.TipoVenta == "Credito").Sum(v => v.Total);

            decimal granTotal = totalEfectivo + totalDigital + totalCredito;
            if (granTotal <= 0) granTotal = 1;

            double pctEfectivo = Math.Round((double)(totalEfectivo / granTotal) * 100.0, 1);
            double pctDigital = Math.Round((double)(totalDigital / granTotal) * 100.0, 1);
            double pctCredito = Math.Round((double)(totalCredito / granTotal) * 100.0, 1);

            pctEfectivo = SanitizarDouble(pctEfectivo, 0, 100, 60.0);
            pctDigital = SanitizarDouble(pctDigital, 0, 100, 25.0);
            pctCredito = SanitizarDouble(pctCredito, 0, 100, 15.0);

            if (ventasMes.Count == 0)
            {
                pctEfectivo = 60.0;
                pctDigital = 25.0;
                pctCredito = 15.0;
                totalEfectivo = 600;
                totalDigital = 250;
                totalCredito = 150;
            }

            MediosPagoDistribucion.Add(new MedioPagoItem
            {
                Nombre = "Efectivo en Mostrador",
                Monto = Math.Round(totalEfectivo, 2),
                Porcentaje = pctEfectivo,
                ColorHex = "#2E7D32",
                Icono = "💵"
            });

            MediosPagoDistribucion.Add(new MedioPagoItem
            {
                Nombre = "Yape / Plin / Digital",
                Monto = Math.Round(totalDigital, 2),
                Porcentaje = pctDigital,
                ColorHex = "#0288D1",
                Icono = "📱"
            });

            MediosPagoDistribucion.Add(new MedioPagoItem
            {
                Nombre = "Fiado / Pendiente Cobrar",
                Monto = Math.Round(totalCredito, 2),
                Porcentaje = pctCredito,
                ColorHex = "#E65100",
                Icono = "💳"
            });
        }

        private void GenerarGraficoCerrucho(List<Venta> ventasMes)
        {
            double canvasWidth = 380.0;
            double canvasHeight = 120.0;
            double paddingY = 15.0;

            List<double> valores = new();
            if (ventasMes != null && ventasMes.Any())
            {
                int count = ventasMes.Count;
                int paso = Math.Max(1, count / 10);
                for (int i = 0; i < count; i += paso)
                {
                    valores.Add((double)ventasMes[i].Total);
                    if (valores.Count >= 10) break;
                }
            }

            while (valores.Count < 10)
            {
                valores.AddRange(new double[] { 150, 45, 280, 90, 340, 110, 420, 180, 390, 220 });
            }
            if (valores.Count > 10) valores = valores.Take(10).ToList();

            double maxVal = valores.Max();
            double minVal = valores.Min();
            if (maxVal == minVal || double.IsNaN(maxVal) || double.IsNaN(minVal))
            {
                maxVal = minVal + 100;
            }

            double range = maxVal - minVal;
            if (range <= 0) range = 100;

            double dx = canvasWidth / Math.Max(1, valores.Count - 1);

            var sbLinea = new StringBuilder();
            var sbRelleno = new StringBuilder();

            // Inicio del polígono de relleno
            sbRelleno.Append(FormatPoint(0, canvasHeight));

            for (int i = 0; i < valores.Count; i++)
            {
                double x = i * dx;
                double yNorm = (valores[i] - minVal) / range;
                yNorm = SanitizarDouble(yNorm, 0, 1, 0.5);

                double y = canvasHeight - paddingY - (yNorm * (canvasHeight - 2 * paddingY));
                y = SanitizarDouble(y, 0, canvasHeight, canvasHeight / 2);

                string ptStr = FormatPoint(x, y);

                if (sbLinea.Length > 0) sbLinea.Append(" ");
                sbLinea.Append(ptStr);

                sbRelleno.Append(" ");
                sbRelleno.Append(ptStr);
            }

            // Cierre del polígono de relleno
            sbRelleno.Append(" ");
            sbRelleno.Append(FormatPoint(canvasWidth, canvasHeight));

            PuntosCerruchoLineaString = sbLinea.ToString();
            PuntosCerruchoRellenoString = sbRelleno.ToString();
        }

        private static string FormatPoint(double x, double y)
        {
            return $"{x.ToString("F1", CultureInfo.InvariantCulture)},{y.ToString("F1", CultureInfo.InvariantCulture)}";
        }

        private static double SanitizarDouble(double val, double min, double max, double defaultVal)
        {
            if (double.IsNaN(val) || double.IsInfinity(val)) return defaultVal;
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
