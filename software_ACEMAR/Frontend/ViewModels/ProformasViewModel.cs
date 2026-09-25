using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class ProformasViewModel : ViewModelBase
    {
        private readonly IProformaService _proformaService;

        private Proforma? _proformaSeleccionada;
        private string _filtroEstado = "Pendiente";

        public ObservableCollection<Proforma> Proformas { get; } = new();

        public ICommand CargarProformasCommand { get; }
        public ICommand AnularProformaCommand { get; }

        public event Action<Proforma>? OnConvertirProformaAVenta;

        public ProformasViewModel(IProformaService proformaService)
        {
            _proformaService = proformaService;

            CargarProformasCommand = new RelayCommand(CargarProformas);
            AnularProformaCommand = new RelayCommand(EjecutarAnularProforma);

            CargarProformas();
        }

        public Proforma? ProformaSeleccionada
        {
            get => _proformaSeleccionada;
            set => SetField(ref _proformaSeleccionada, value);
        }

        public string FiltroEstado
        {
            get => _filtroEstado;
            set
            {
                if (SetField(ref _filtroEstado, value))
                {
                    CargarProformas();
                }
            }
        }

        public void CargarProformas()
        {
            var todas = _proformaService.ObtenerTodas();
            Proformas.Clear();

            var filtradas = FiltroEstado == "Todas" 
                ? todas 
                : todas.Where(p => string.Equals(p.Estado, FiltroEstado, StringComparison.OrdinalIgnoreCase));

            foreach (var p in filtradas)
            {
                Proformas.Add(p);
            }
        }

        public void SolicitarConvertirAVenta(Proforma p)
        {
            if (p.Estado != "Pendiente")
            {
                MessageBox.Show($"Esta proforma ya se encuentra en estado '{p.Estado}'.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OnConvertirProformaAVenta?.Invoke(p);
        }

        private void EjecutarAnularProforma(object? param)
        {
            var p = param as Proforma ?? ProformaSeleccionada;
            if (p == null) return;

            if (p.Estado != "Pendiente")
            {
                MessageBox.Show($"Solo se pueden anular proformas en estado 'Pendiente'.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"¿Desea anular la proforma {p.NumeroProforma}?", "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _proformaService.AnularProforma(p.Id);
            CargarProformas();
        }
    }
}
