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

            // Inicializar base de datos local SQLite y seed de datos de prueba
            try
            {
                using var context = new AppDbContext();
                context.InitializeDatabase();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al inicializar la base de datos local SQLite: {ex.Message}", "Error de Inicialización ACEMAR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
