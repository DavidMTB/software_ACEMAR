using System.Windows;
using software_ACEMAR.Data;

namespace software_ACEMAR
{

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

            try
            {
                using var context = new AppDbContext();
                context.InitializeDatabase();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al inicializar la base de datos local SQLite: {ex.Message}", "Error de Inicialización ACEMAR", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var authService = new software_ACEMAR.Services.AuthService();
            var loginWindow = new software_ACEMAR.Views.LoginWindow(authService);
            bool? loginExitoso = loginWindow.ShowDialog();

            if (loginExitoso != true)
            {

                Shutdown();
                return;
            }

            var mainVm = new software_ACEMAR.ViewModels.MainViewModel(authService);
            var mainWindow = new MainWindow(mainVm);

            this.MainWindow = mainWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
    }
}
