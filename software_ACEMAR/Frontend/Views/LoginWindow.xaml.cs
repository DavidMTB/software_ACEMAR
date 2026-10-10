using System.Windows;
using software_ACEMAR.Services;

namespace software_ACEMAR.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IAuthService _authService;

        public LoginWindow(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService;
            txtUsuario.Focus();
        }

        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Password;

            if (_authService.IniciarSesion(usuario, password, out string error))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                txtError.Text = error;
                pnlError.Visibility = Visibility.Visible;
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }

        private void BtnSalir_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
