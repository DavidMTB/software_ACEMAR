using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class UsuariosViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        private Usuario? _usuarioSeleccionado;
        private string _busquedaTexto = string.Empty;

        // Propiedades de Formulario de Registro / Edición
        private int _formId;
        private string _formNombreUsuario = string.Empty;
        private string _formPassword = string.Empty;
        private string _formMostradorAsignado = "Mostrador 01";
        private bool _esModoEdicion;
        private bool _mostrarFormularioModal;
        private string _mensajeErrorForm = string.Empty;

        public ObservableCollection<Usuario> Usuarios { get; } = new();
        public ObservableCollection<string> MostradoresDisponibles { get; } = new()
        {
            "Mostrador 01",
            "Mostrador 02",
            "Mostrador 03",
            "Mostrador 04",
            "Mostrador 05",
            "Caja Central"
        };

        public ICommand AbrirNuevoUsuarioCommand { get; }
        public ICommand GuardarUsuarioCommand { get; }
        public ICommand EditarUsuarioCommand { get; }
        public ICommand EliminarUsuarioCommand { get; }
        public ICommand CancelarFormularioCommand { get; }
        public ICommand CargarUsuariosCommand { get; }

        public UsuariosViewModel(IUserService userService, IAuthService authService)
        {
            _userService = userService;
            _authService = authService;

            AbrirNuevoUsuarioCommand = new RelayCommand(EjecutarAbrirNuevoUsuario);
            GuardarUsuarioCommand = new RelayCommand(async () => await EjecutarGuardarUsuarioAsync());
            EditarUsuarioCommand = new RelayCommand(EjecutarEditarUsuario);
            EliminarUsuarioCommand = new RelayCommand(async () => await EjecutarEliminarUsuarioAsync());
            CancelarFormularioCommand = new RelayCommand(CerrarFormularioModal);
            CargarUsuariosCommand = new RelayCommand(async () => await CargarUsuariosAsync());

            _ = CargarUsuariosAsync();
        }

        public Usuario? UsuarioSeleccionado
        {
            get => _usuarioSeleccionado;
            set => SetField(ref _usuarioSeleccionado, value);
        }

        public string BusquedaTexto
        {
            get => _busquedaTexto;
            set
            {
                if (SetField(ref _busquedaTexto, value))
                {
                    _ = CargarUsuariosAsync();
                }
            }
        }

        public int FormId
        {
            get => _formId;
            set => SetField(ref _formId, value);
        }

        public string FormNombreUsuario
        {
            get => _formNombreUsuario;
            set => SetField(ref _formNombreUsuario, value);
        }

        public string FormPassword
        {
            get => _formPassword;
            set => SetField(ref _formPassword, value);
        }

        public string FormMostradorAsignado
        {
            get => _formMostradorAsignado;
            set => SetField(ref _formMostradorAsignado, value);
        }

        public bool EsModoEdicion
        {
            get => _esModoEdicion;
            set => SetField(ref _esModoEdicion, value);
        }

        public bool MostrarFormularioModal
        {
            get => _mostrarFormularioModal;
            set => SetField(ref _mostrarFormularioModal, value);
        }

        public string MensajeErrorForm
        {
            get => _mensajeErrorForm;
            set => SetField(ref _mensajeErrorForm, value);
        }

        public bool EsAdministrador => string.Equals(_authService.UsuarioActual.Rol, "Administrador", StringComparison.OrdinalIgnoreCase);

        public async Task CargarUsuariosAsync()
        {
            var lista = await _userService.ObtenerTodosUsuariosAsync();

            if (!string.IsNullOrWhiteSpace(BusquedaTexto))
            {
                string query = BusquedaTexto.Trim().ToLower();
                lista = lista.Where(u => u.NombreUsuario.ToLower().Contains(query) || u.NombreCompleto.ToLower().Contains(query)).ToList();
            }

            Usuarios.Clear();
            foreach (var u in lista)
            {
                Usuarios.Add(u);
            }
        }

        private void EjecutarAbrirNuevoUsuario()
        {
            FormId = 0;
            FormNombreUsuario = string.Empty;
            FormPassword = string.Empty;
            FormMostradorAsignado = "Mostrador 01";
            EsModoEdicion = false;
            MensajeErrorForm = string.Empty;
            MostrarFormularioModal = true;
        }

        private void EjecutarEditarUsuario()
        {
            if (UsuarioSeleccionado == null)
            {
                MessageBox.Show("Seleccione un usuario de la lista para editar.", "Gestión de Usuarios", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            FormId = UsuarioSeleccionado.Id;
            FormNombreUsuario = UsuarioSeleccionado.NombreUsuario;
            FormPassword = UsuarioSeleccionado.Password;
            FormMostradorAsignado = string.IsNullOrWhiteSpace(UsuarioSeleccionado.MostradorAsignado) ? "Mostrador 01" : UsuarioSeleccionado.MostradorAsignado;
            EsModoEdicion = true;
            MensajeErrorForm = string.Empty;
            MostrarFormularioModal = true;
        }

        private async Task EjecutarGuardarUsuarioAsync()
        {
            MensajeErrorForm = string.Empty;

            if (string.IsNullOrWhiteSpace(FormNombreUsuario))
            {
                MensajeErrorForm = "Ingrese el nombre del vendedor.";
                return;
            }

            if (string.IsNullOrWhiteSpace(FormPassword))
            {
                MensajeErrorForm = "Ingrese la contraseña.";
                return;
            }

            if (EsModoEdicion)
            {
                var (exito, error) = await _userService.ActualizarUsuarioAsync(FormId, FormNombreUsuario, FormPassword, FormMostradorAsignado);
                if (!exito)
                {
                    MensajeErrorForm = error;
                    return;
                }
                MessageBox.Show("Datos del vendedor y mostrador asignado actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var (exito, error, _) = await _userService.RegistrarVendedorAsync(FormNombreUsuario, FormPassword, FormMostradorAsignado);
                if (!exito)
                {
                    MensajeErrorForm = error;
                    return;
                }
                MessageBox.Show($"Vendedor '{FormNombreUsuario}' asignado a {FormMostradorAsignado} registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            CerrarFormularioModal();
            await CargarUsuariosAsync();
        }

        private async Task EjecutarEliminarUsuarioAsync()
        {
            if (UsuarioSeleccionado == null)
            {
                MessageBox.Show("Seleccione un vendedor de la lista para eliminar.", "Gestión de Usuarios", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (UsuarioSeleccionado.Rol == "Administrador" || string.Equals(UsuarioSeleccionado.NombreUsuario, "Gerente", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("No se puede eliminar la cuenta principal de Administrador por seguridad del sistema.", "Acceso Restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"¿Está seguro de que desea eliminar al vendedor '{UsuarioSeleccionado.NombreUsuario}'?",
                "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                var (exito, error) = await _userService.EliminarUsuarioAsync(UsuarioSeleccionado.Id);
                if (!exito)
                {
                    MessageBox.Show(error, "Error al Eliminar", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("Vendedor eliminado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                await CargarUsuariosAsync();
            }
        }

        private void CerrarFormularioModal()
        {
            MostrarFormularioModal = false;
            MensajeErrorForm = string.Empty;
        }
    }
}
