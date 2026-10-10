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
        private readonly ILoteService _loteService;
        private readonly IProveedorService _proveedorService;
        private readonly IUserService _userService;
        private readonly IVentaCorrectionService _ventaCorrectionService;
        private readonly IPdfService _pdfService;

        private ViewModelBase _currentViewModel;
        private string _tituloVistaActual = "Punto de Venta (POS)";
        private string _rolActual = "Administrador";
        private string _nombreUsuarioActual = "Gerente General ACEMAR";

        public PosViewModel PosVM { get; }
        public DashboardGeneralViewModel DashboardGeneralVM { get; }
        public DashboardViewModel DashboardVM { get; }
        public ProductCrudViewModel ProductCrudVM { get; }
        public CreditsViewModel CreditsVM { get; }
        public ProformasViewModel ProformasVM { get; }
        public CashRegisterViewModel CashRegisterVM { get; }
        public LotesViewModel LotesVM { get; }
        public ProveedoresViewModel ProveedoresVM { get; }
        public VentasCorreccionViewModel VentasCorreccionVM { get; }
        public UsuariosViewModel UsuariosVM { get; }

        public ICommand NavegarPosCommand { get; }
        public ICommand NavegarDashboardGeneralCommand { get; }
        public ICommand NavegarDashboardCommand { get; }
        public ICommand NavegarProductCrudCommand { get; }
        public ICommand NavegarCreditsCommand { get; }
        public ICommand NavegarProformasCommand { get; }
        public ICommand NavegarCashRegisterCommand { get; }
        public ICommand NavegarLotesCommand { get; }
        public ICommand NavegarProveedoresCommand { get; }
        public ICommand NavegarVentasCorreccionCommand { get; }
        public ICommand NavegarUsuariosCommand { get; }
        public ICommand AlternarModoAdminCommand { get; }
        public ICommand CerrarSesionCommand { get; }

        public MainViewModel() : this(new AuthService())
        {
        }

        public MainViewModel(IAuthService authService)
        {
            // Inicializar servicios
            _authService = authService;
            _inventoryService = new InventoryService();
            _salesService = new SalesService();
            _creditService = new CreditService(_authService);
            _proformaService = new ProformaService();
            _cashRegisterService = new CashRegisterService();
            _loteService = new LoteService();
            _proveedorService = new ProveedorService();
            _ventaCorrectionService = new VentaCorrectionService();
            _pdfService = new PdfService();
            _userService = new UserService();

            // Inicializar ViewModels
            PosVM = new PosViewModel(_inventoryService, _salesService, _authService, _creditService, _proformaService, _pdfService);
            DashboardGeneralVM = new DashboardGeneralViewModel(_inventoryService, _salesService, _creditService, _proformaService, _cashRegisterService, _loteService);
            DashboardVM = new DashboardViewModel(_inventoryService, _salesService);
            ProductCrudVM = new ProductCrudViewModel(_inventoryService);
            CreditsVM = new CreditsViewModel(_creditService, _authService);
            ProformasVM = new ProformasViewModel(_proformaService, _pdfService);
            CashRegisterVM = new CashRegisterViewModel(_cashRegisterService, _authService);
            LotesVM = new LotesViewModel(_loteService, _inventoryService, _proveedorService);
            ProveedoresVM = new ProveedoresViewModel(_proveedorService, _inventoryService);
            VentasCorreccionVM = new VentasCorreccionViewModel(_ventaCorrectionService, _salesService, _pdfService);
            UsuariosVM = new UsuariosViewModel(_userService, _authService);

            // Suscribir eventos de navegación de DashboardGeneral
            DashboardGeneralVM.OnNavegarSolicitado += vista =>
            {
                switch (vista)
                {
                    case "POS": NavegarAPos(); break;
                    case "Dashboard": NavegarADashboardGeneral(); break;
                    case "Inventario": NavegarADashboard(); break;
                    case "Catalogo": NavegarAProductCrud(); break;
                    case "Creditos": NavegarACredits(); break;
                    case "Proformas": NavegarAProformas(); break;
                    case "Caja": NavegarACashRegister(); break;
                    case "Lotes": NavegarALotes(); break;
                    case "Proveedores": NavegarAProveedores(); break;
                    case "Auditoria": NavegarAVentasCorreccion(); break;
                    case "Usuarios": NavegarAUsuarios(); break;
                }
            };

            // Suscribir eventos de navegación de DashboardVM (Inventario)
            DashboardVM.OnNavegarSolicitado += vista =>
            {
                if (vista == "Lotes") NavegarALotes();
            };

            // Suscribir conversión de proforma a venta en POS
            ProformasVM.OnConvertirProformaAVenta += ProformaAConvertir =>
            {
                PosVM.CargarItemsDesdeProforma(ProformaAConvertir);
                NavegarAPos();
            };

            _currentViewModel = PosVM;

            // Comandos de navegación
            NavegarPosCommand = new RelayCommand(NavegarAPos);
            NavegarDashboardGeneralCommand = new RelayCommand(NavegarADashboardGeneral);
            NavegarDashboardCommand = new RelayCommand(NavegarADashboard);
            NavegarProductCrudCommand = new RelayCommand(NavegarAProductCrud);
            NavegarCreditsCommand = new RelayCommand(NavegarACredits);
            NavegarProformasCommand = new RelayCommand(NavegarAProformas);
            NavegarCashRegisterCommand = new RelayCommand(NavegarACashRegister);
            NavegarLotesCommand = new RelayCommand(NavegarALotes);
            NavegarProveedoresCommand = new RelayCommand(NavegarAProveedores);
            NavegarVentasCorreccionCommand = new RelayCommand(NavegarAVentasCorreccion);
            NavegarUsuariosCommand = new RelayCommand(NavegarAUsuarios);
            AlternarModoAdminCommand = new RelayCommand(EjecutarAlternarModoAdmin);
            CerrarSesionCommand = new RelayCommand(EjecutarCerrarSesion);

            _authService.OnUsuarioCambiado += ActualizarDatosUsuario;
            ActualizarDatosUsuario();
        }

        public void SetAuthService(IAuthService authService)
        {
            // Método helper para inyección de instancia compartida de auth
            ActualizarDatosUsuario();
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (SetField(ref _currentViewModel, value))
                {
                    OnPropertyChanged(nameof(IsPosActive));
                    OnPropertyChanged(nameof(IsDashboardGeneralActive));
                    OnPropertyChanged(nameof(IsDashboardActive));
                    OnPropertyChanged(nameof(IsProductCrudActive));
                    OnPropertyChanged(nameof(IsCreditsActive));
                    OnPropertyChanged(nameof(IsProformasActive));
                    OnPropertyChanged(nameof(IsCashRegisterActive));
                    OnPropertyChanged(nameof(IsLotesActive));
                    OnPropertyChanged(nameof(IsProveedoresActive));
                    OnPropertyChanged(nameof(IsVentasCorreccionActive));
                    OnPropertyChanged(nameof(IsUsuariosActive));
                }
            }
        }

        public bool IsPosActive => CurrentViewModel is PosViewModel;
        public bool IsDashboardGeneralActive => CurrentViewModel is DashboardGeneralViewModel;
        public bool IsDashboardActive => CurrentViewModel is DashboardViewModel;
        public bool IsProductCrudActive => CurrentViewModel is ProductCrudViewModel;
        public bool IsCreditsActive => CurrentViewModel is CreditsViewModel;
        public bool IsProformasActive => CurrentViewModel is ProformasViewModel;
        public bool IsCashRegisterActive => CurrentViewModel is CashRegisterViewModel;
        public bool IsLotesActive => CurrentViewModel is LotesViewModel;
        public bool IsProveedoresActive => CurrentViewModel is ProveedoresViewModel;
        public bool IsVentasCorreccionActive => CurrentViewModel is VentasCorreccionViewModel;
        public bool IsUsuariosActive => CurrentViewModel is UsuariosViewModel;

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

        public string MostradorActual => string.IsNullOrWhiteSpace(_authService.UsuarioActual?.MostradorAsignado) ? "Mostrador 01" : _authService.UsuarioActual.MostradorAsignado;

        private void ActualizarDatosUsuario()
        {
            RolActual = _authService.UsuarioActual.Rol;
            NombreUsuarioActual = _authService.UsuarioActual.NombreCompleto;
            OnPropertyChanged(nameof(EsAdmin));
            OnPropertyChanged(nameof(MostradorActual));
        }

        private void NavegarAPos()
        {
            PosVM.CargarProductos();
            PosVM.CargarClientesCredito();
            CurrentViewModel = PosVM;
            TituloVistaActual = "Punto de Venta (POS) - Despacho Multimedida & FIFO";
        }

        private void NavegarADashboardGeneral()
        {
            DashboardGeneralVM.CargarDatosDashboard();
            CurrentViewModel = DashboardGeneralVM;
            TituloVistaActual = "Dashboard Analítico & Centro de Control Ejecutivo";
        }

        private void NavegarADashboard()
        {
            DashboardVM.CargarInventario();
            CurrentViewModel = DashboardVM;
            TituloVistaActual = "Inventario Inteligente, CuMax y Valorización";
        }

        private void NavegarAProductCrud()
        {
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
            if (!_authService.PuedeAccederCreditos(out string mensajeError))
            {
                MessageBox.Show(mensajeError, "Seguridad - ACEMAR", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreditsVM.VerificarPermisos();
            CurrentViewModel = CreditsVM;
            TituloVistaActual = "Gestión de Créditos y Fiados (Límite Individual de Deuda)";
        }

        private void NavegarAProformas()
        {
            ProformasVM.CargarProformas();
            CurrentViewModel = ProformasVM;
            TituloVistaActual = "Proformas y Cotizaciones Comerciales";
        }

        private void NavegarACashRegister()
        {
            CashRegisterVM.ActualizarBalance();
            CurrentViewModel = CashRegisterVM;
            TituloVistaActual = "Arqueo y Control de Caja Diario";
        }

        private void NavegarALotes()
        {
            if (!EsAdmin)
            {
                if (!SolicitarAutenticacionGerente()) return;
            }

            LotesVM.CargarLotes();
            CurrentViewModel = LotesVM;
            TituloVistaActual = "Gestión de Lotes, Vencimientos y Stock FIFO";
        }

        private void NavegarAProveedores()
        {
            if (!EsAdmin)
            {
                if (!SolicitarAutenticacionGerente()) return;
            }

            ProveedoresVM.CargarProveedores();
            CurrentViewModel = ProveedoresVM;
            TituloVistaActual = "Gestión de Proveedores y Tiempos de Reposición";
        }

        private void NavegarAVentasCorreccion()
        {
            if (!EsAdmin)
            {
                if (!SolicitarAutenticacionGerente()) return;
            }

            VentasCorreccionVM.CargarVentas();
            CurrentViewModel = VentasCorreccionVM;
            TituloVistaActual = "Auditoría y Corrección de Ventas Procesadas";
        }

        private void NavegarAUsuarios()
        {
            if (!EsAdmin)
            {
                MessageBox.Show("Acceso Denegado. La gestión de usuarios y vendedores es exclusiva del Administrador.", "Seguridad ACEMAR", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (!SolicitarAutenticacionGerente()) return;
            }

            _ = UsuariosVM.CargarUsuariosAsync();
            CurrentViewModel = UsuariosVM;
            TituloVistaActual = "Gestión de Usuarios, Permisos y Vendedores";
        }

        private void EjecutarCerrarSesion()
        {
            var resp = MessageBox.Show("¿Está seguro de que desea cerrar la sesión activa?", "Cerrar Sesión", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp == MessageBoxResult.Yes)
            {
                _authService.CerrarSesion();
                var loginWindow = new LoginWindow(_authService);
                bool? result = loginWindow.ShowDialog();
                if (result != true)
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    NavegarAPos();
                }
            }
        }

        private void EjecutarAlternarModoAdmin()
        {
            if (EsAdmin)
            {
                var resp = MessageBox.Show("¿Desea cambiar la sesión al modo 'Vendedor'?", "Cambio de Rol", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (resp == MessageBoxResult.Yes)
                {
                    bool cambiado = _authService.CerrarSesionAdmin(out bool requiereLogin);
                    if (requiereLogin || !cambiado)
                    {
                        var loginWindow = new LoginWindow(_authService);
                        if (Application.Current?.MainWindow != null)
                        {
                            loginWindow.Owner = Application.Current.MainWindow;
                        }
                        loginWindow.ShowDialog();
                    }

                    if (CurrentViewModel is CreditsViewModel || CurrentViewModel is ProductCrudViewModel || CurrentViewModel is LotesViewModel || CurrentViewModel is ProveedoresViewModel || CurrentViewModel is VentasCorreccionViewModel || CurrentViewModel is UsuariosViewModel)
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
