using System.Windows;
using software_ACEMAR.Data;

namespace software_ACEMAR
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Excepción no controlada en la aplicación:\n\n{args.Exception.Message}\n\nDetalles:\n{args.Exception}", 
                                "Error Crítico ACEMAR", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // 1. Inicializar la base de datos local SQLite y crear tablas PRIMERO
            try
            {
                using var context = new AppDbContext();
                context.InitializeDatabase();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al inicializar la base de datos local SQLite: {ex.Message}", "Error de Inicialización ACEMAR", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 2. Desactivar el apagado automático de WPF durante el inicio de sesión
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var authService = new software_ACEMAR.Services.AuthService();
            var loginWindow = new software_ACEMAR.Views.LoginWindow(authService);
            bool? loginExitoso = loginWindow.ShowDialog();

            if (loginExitoso != true)
            {
                // Si se cancela el login o se cierra la ventana, salir de la aplicación
                Shutdown();
                return;
            }

            // 3. Instanciar y mostrar la ventana principal con la sesión del usuario autenticado
            var mainVm = new software_ACEMAR.ViewModels.MainViewModel(authService);
            var mainWindow = new MainWindow(mainVm);

            this.MainWindow = mainWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
    }
}
