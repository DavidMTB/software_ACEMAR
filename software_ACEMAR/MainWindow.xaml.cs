using System.Windows;
using software_ACEMAR.ViewModels;

namespace software_ACEMAR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}