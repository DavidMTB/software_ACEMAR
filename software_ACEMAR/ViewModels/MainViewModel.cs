using System;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;
using software_ACEMAR.Views;

namespace software_ACEMAR.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IInventoryService _inventoryService;
        private readonly ISalesService _salesService;
        private readonly ICreditService _creditService;
        private readonly IProformaService _proformaService;
        private readonly ICashRegisterService _cashRegisterService;

        private ViewModelBase _currentViewModel;
        private string _tituloVistaActual = "Punto de Venta (POS)";
        private string _rolActual = "Administrador";
        private string _nombreUsuarioActual = "Gerente General ACEMAR";

        public PosViewModel PosVM { get; }
        public DashboardViewModel DashboardVM { get; }
        public ProductCrudViewModel ProductCrudVM { get; }
        public CreditsViewModel CreditsVM { get; }
        public ProformasViewModel ProformasVM { get; }
        public CashRegisterViewModel CashRegisterVM { get; }

        public ICommand NavegarPosCommand { get; }
        public ICommand NavegarDashboardCommand { get; }
        public ICommand NavegarProductCrudCommand { get; }
        public ICommand NavegarCreditsCommand { get; }
        public ICommand NavegarProformasCommand { get; }
        public ICommand NavegarCashRegisterCommand { get; }
        public ICommand AlternarModoAdminCommand { get; }

        public MainViewModel()
        {
            // Inicializar servicios
            _authService = new AuthService();
            _inventoryService = new InventoryService();
            _salesService = new SalesService();
            _creditService = new CreditService(_authService);
            _proformaService = new ProformaService();
            _cashRegisterService = new CashRegisterService();

            // Inicializar ViewModels
            PosVM = new PosViewModel(_inventoryService, _salesService, _authService, _creditService, _proformaService);
            DashboardVM = new DashboardViewModel(_inventoryService);
            ProductCrudVM = new ProductCrudViewModel(_inventoryService);
            CreditsVM = new CreditsViewModel(_creditService, _authService);
            ProformasVM = new ProformasViewModel(_proformaService);
            CashRegisterVM = new CashRegisterViewModel(_cashRegisterService, _authService);

            // Suscribir conversión de proforma a venta en POS
            ProformasVM.OnConvertirProformaAVenta += ProformaAConvertir =>
            {
                PosVM.CargarItemsDesdeProforma(ProformaAConvertir);
                NavegarAPos();
            };

            _currentViewModel = PosVM;

            // Comandos de navegación
            NavegarPosCommand = new RelayCommand(NavegarAPos);
            NavegarDashboardCommand = new RelayCommand(NavegarADashboard);
            NavegarProductCrudCommand = new RelayCommand(NavegarAProductCrud);
            NavegarCreditsCommand = new RelayCommand(NavegarACredits);
            NavegarProformasCommand = new RelayCommand(NavegarAProformas);
            NavegarCashRegisterCommand = new RelayCommand(NavegarACashRegister);
            AlternarModoAdminCommand = new RelayCommand(EjecutarAlternarModoAdmin);

            _authService.OnUsuarioCambiado += ActualizarDatosUsuario;
            ActualizarDatosUsuario();
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetField(ref _currentViewModel, value);
        }

        public string TituloVistaActual
        {
            get => _tituloVistaActual;
            set => SetField(ref _tituloVistaActual, value);
        }

        public string RolActual
        {
            get => _rolActual;
            set => SetField(ref _rolActual, value);
        }

        public string NombreUsuarioActual
        {
            get => _nombreUsuarioActual;
            set => SetField(ref _nombreUsuarioActual, value);
        }

        public bool EsAdmin => string.Equals(_rolActual, "Administrador", StringComparison.OrdinalIgnoreCase);

        private void ActualizarDatosUsuario()
        {
            RolActual = _authService.UsuarioActual.Rol;
            NombreUsuarioActual = _authService.UsuarioActual.NombreCompleto;
            OnPropertyChanged(nameof(EsAdmin));
        }

        private void NavegarAPos()
        {
            PosVM.CargarProductos();
            PosVM.CargarClientesCredito();
            CurrentViewModel = PosVM;
            TituloVistaActual = "Punto de Venta (POS)";
        }

        private void NavegarADashboard()
        {
            DashboardVM.CargarInventario();
            CurrentViewModel = DashboardVM;
            TituloVistaActual = "Inventario Inteligente y Almacén";
        }

        private void NavegarAProductCrud()
        {
            // Requiere permisos de Administrador / Gerente
            if (!EsAdmin)
            {
                if (!SolicitarAutenticacionGerente()) return;
            }

            ProductCrudVM.CargarProductos();
            CurrentViewModel = ProductCrudVM;
            TituloVistaActual = "Catálogo de Productos y Precios Masivos";
        }

        private void NavegarACredits()
        {
            // Regla CRE-01 (Seguridad Fiados)
            if (!_authService.PuedeAccederCreditos(out string mensajeError))
            {
                MessageBox.Show(mensajeError, "Seguridad - Ferretería ACEMAR", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreditsVM.VerificarPermisos();
            CurrentViewModel = CreditsVM;
            TituloVistaActual = "Gestión de Créditos y Fiados";
        }

        private void NavegarAProformas()
        {
            ProformasVM.CargarProformas();
            CurrentViewModel = ProformasVM;
            TituloVistaActual = "Proformas y Cotizaciones para Clientes";
        }

        private void NavegarACashRegister()
        {
            CashRegisterVM.ActualizarBalance();
            CurrentViewModel = CashRegisterVM;
            TituloVistaActual = "Arqueo y Control de Caja Diario";
        }

        private void EjecutarAlternarModoAdmin()
        {
            if (EsAdmin)
            {
                var resp = MessageBox.Show("¿Desea cambiar la sesión al modo 'Vendedor'?", "Cambio de Rol", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (resp == MessageBoxResult.Yes)
                {
                    _authService.CerrarSesionAdmin();
                    if (CurrentViewModel is CreditsViewModel || CurrentViewModel is ProductCrudViewModel)
                    {
                        NavegarAPos();
                    }
                }
            }
            else
            {
                SolicitarAutenticacionGerente();
            }
        }

        private bool SolicitarAutenticacionGerente()
        {
            var dialog = new LoginAdminDialog(_authService);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                MessageBox.Show("¡Acceso como Administrador autorizado con éxito!", "Sesión Iniciada", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            return false;
        }
    }
}
