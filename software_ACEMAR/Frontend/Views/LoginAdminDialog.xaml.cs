using System.Windows;
using software_ACEMAR.Services;

namespace software_ACEMAR.Views
{
    public partial class LoginAdminDialog : Window
    {
        private readonly IAuthService _authService;

        public LoginAdminDialog(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService;
            txtUsuario.Text = "Gerente";
            txtPassword.Focus();
        }

        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text;
            string password = txtPassword.Password;

            if (_authService.IniciarSesionAdmin(usuario, password, out string error))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                txtError.Text = error;
                txtError.Visibility = Visibility.Visible;
                txtPassword.SelectAll();
                txtPassword.Focus();
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
