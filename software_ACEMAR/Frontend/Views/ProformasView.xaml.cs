using System.Windows.Controls;

namespace software_ACEMAR.Views
{
    public partial class ProformasView : UserControl
    {
        public ProformasView()
        {
            InitializeComponent();
        }

        private void BtnConvertir_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ProformasViewModel vm && vm.ProformaSeleccionada != null)
            {
                vm.SolicitarConvertirAVenta(vm.ProformaSeleccionada);
            }
        }
    }
}
