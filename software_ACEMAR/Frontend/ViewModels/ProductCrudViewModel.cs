using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class UnidadMedidaPrecioItem : ViewModelBase
    {
        private bool _estaSeleccionada;
        private string _nombreUnidad = string.Empty;
        private string _precioTexto = "0.00";
        private string _factorTexto = "1.00";

        public bool EstaSeleccionada
        {
            get => _estaSeleccionada;
            set => SetField(ref _estaSeleccionada, value);
        }

        public string NombreUnidad
        {
            get => _nombreUnidad;
            set => SetField(ref _nombreUnidad, value);
        }

        public string PrecioTexto
        {
            get => _precioTexto;
            set => SetField(ref _precioTexto, value);
        }

        public string FactorTexto
        {
            get => _factorTexto;
            set => SetField(ref _factorTexto, value);
        }

        public decimal ObtenerPrecio()
        {
            if (decimal.TryParse(PrecioTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal p) && p > 0)
                return p;
            return 0m;
        }

        public decimal ObtenerFactor()
        {
            if (decimal.TryParse(FactorTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal f) && f > 0)
                return f;
            return 1.0m;
        }
    }

    public class ProductCrudViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;

        private Producto? _productoSeleccionado;
        private string _busquedaTexto = string.Empty;

        // Formulario de edición/creación
        private int _formId;
        private string _formCodigo = string.Empty;
        private string _formNombre = string.Empty;
        private string _formCategoria = "Construcción";
        private string _formPrecio = "10.00";
        private string _formStockActual = "10";
        private string _formStockMinimo = "5";
        private string _formCuMax = "50";
        private string _formUnidad = "Unidad";
        private string _formUnidadesPermitidas = string.Empty;
        private string _nuevaUnidadNombre = string.Empty;
        private bool _esModoEdicion;

        // Actualización masiva de precios
        private string _categoriaAumento = "Todas";
        private string _porcentajeAumentoTexto = "5.0";

        public ObservableCollection<Producto> Productos { get; } = new();
        public ObservableCollection<string> CategoriasDisponibles { get; } = new();
        public ObservableCollection<UnidadMedidaPrecioItem> ListaUnidadesPrecios { get; } = new();

        public ObservableCollection<string> UnidadesMedida { get; } = new()
        {
            "Unidad", "Bolsa", "Saco", "Caja", "Kg", "Metros", "Ciento", "Galón", "Rollo", "Millar", "Lata", "Carretilla", "Volquetada", "Paquete"
        };

        public ICommand GuardarProductoCommand { get; }
        public ICommand EditarProductoCommand { get; }
        public ICommand EliminarProductoCommand { get; }
        public ICommand LimpiarFormularioCommand { get; }
        public ICommand AplicarAumentoMasivoCommand { get; }
        public ICommand RefrescarCommand { get; }
        public ICommand AgregarNuevaUnidadCommand { get; }

        public ProductCrudViewModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;

            GuardarProductoCommand = new RelayCommand(EjecutarGuardarProducto);
            EditarProductoCommand = new RelayCommand(EjecutarEditarProducto);
            EliminarProductoCommand = new RelayCommand(EjecutarEliminarProducto);
            LimpiarFormularioCommand = new RelayCommand(LimpiarFormulario);
            AplicarAumentoMasivoCommand = new RelayCommand(EjecutarAumentoMasivo);
            RefrescarCommand = new RelayCommand(CargarProductos);
            AgregarNuevaUnidadCommand = new RelayCommand(EjecutarAgregarNuevaUnidad);

            CargarProductos();
            InicializarListaUnidadesPrecios(null);
        }

        public string BusquedaTexto
        {
            get => _busquedaTexto;
            set
            {
                if (SetField(ref _busquedaTexto, value))
                {
                    CargarProductos();
                }
            }
        }

        public Producto? ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set
            {
                if (SetField(ref _productoSeleccionado, value) && value != null)
                {
                    CargarDatosEnFormulario(value);
                }
            }
        }

        public int FormId { get => _formId; set => SetField(ref _formId, value); }
        public string FormCodigo { get => _formCodigo; set => SetField(ref _formCodigo, value); }
        public string FormNombre { get => _formNombre; set => SetField(ref _formNombre, value); }
        public string FormCategoria { get => _formCategoria; set => SetField(ref _formCategoria, value); }
        public string FormPrecio { get => _formPrecio; set => SetField(ref _formPrecio, value); }
        public string FormStockActual { get => _formStockActual; set => SetField(ref _formStockActual, value); }
        public string FormStockMinimo { get => _formStockMinimo; set => SetField(ref _formStockMinimo, value); }
        public string FormCuMax { get => _formCuMax; set => SetField(ref _formCuMax, value); }
        public string FormUnidad { get => _formUnidad; set => SetField(ref _formUnidad, value); }
        public string FormUnidadesPermitidas { get => _formUnidadesPermitidas; set => SetField(ref _formUnidadesPermitidas, value); }
        public string NuevaUnidadNombre { get => _nuevaUnidadNombre; set => SetField(ref _nuevaUnidadNombre, value); }
        public bool EsModoEdicion { get => _esModoEdicion; set => SetField(ref _esModoEdicion, value); }

        public string CategoriaAumento { get => _categoriaAumento; set => SetField(ref _categoriaAumento, value); }
        public string PorcentajeAumentoTexto { get => _porcentajeAumentoTexto; set => SetField(ref _porcentajeAumentoTexto, value); }

        public void CargarProductos()
        {
            var lista = _inventoryService.BuscarPorNombre(BusquedaTexto);
            Productos.Clear();
            foreach (var p in lista)
            {
                Productos.Add(p);
            }

            CategoriasDisponibles.Clear();
            CategoriasDisponibles.Add("Todas");
            foreach (var cat in _inventoryService.ObtenerCategorias())
            {
                CategoriasDisponibles.Add(cat);
            }
        }

        private void InicializarListaUnidadesPrecios(Producto? p)
        {
            ListaUnidadesPrecios.Clear();

            var permitidas = p?.ObtenerUnidadesDisponibles() ?? new List<string> { "Unidad" };
            var precios = p?.ObtenerPreciosPorUnidad() ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            var listaBase = UnidadesMedida.ToList();
            if (p != null)
            {
                foreach (var u in permitidas)
                {
                    if (!listaBase.Contains(u, StringComparer.OrdinalIgnoreCase))
                        listaBase.Add(u);
                }
            }

            foreach (var u in listaBase)
            {
                bool seleccionada = permitidas.Contains(u, StringComparer.OrdinalIgnoreCase) || string.Equals(u, p?.UnidadMedida, StringComparison.OrdinalIgnoreCase);
                if (p == null && string.Equals(u, "Unidad", StringComparison.OrdinalIgnoreCase))
                {
                    seleccionada = true;
                }

                decimal precio = 0m;
                if (precios.TryGetValue(u, out decimal pr) && pr > 0)
                {
                    precio = pr;
                }
                else if (p != null)
                {
                    precio = p.PrecioUnitario;
                }
                else
                {
                    decimal.TryParse(FormPrecio.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out precio);
                }

                decimal factor = p != null ? p.ObtenerFactorParaUnidad(u) : 1.0m;
                if (p == null)
                {
                    if (string.Equals(u, "Volquetada", StringComparison.OrdinalIgnoreCase)) factor = 4.0m;
                    else if (string.Equals(u, "Ciento", StringComparison.OrdinalIgnoreCase)) factor = 100.0m;
                    else if (string.Equals(u, "Millar", StringComparison.OrdinalIgnoreCase)) factor = 1000.0m;
                    else if (string.Equals(u, "Par", StringComparison.OrdinalIgnoreCase)) factor = 2.0m;
                    else if (string.Equals(u, "Carretilla", StringComparison.OrdinalIgnoreCase)) factor = 0.25m;
                    else if (string.Equals(u, "Lata", StringComparison.OrdinalIgnoreCase)) factor = 0.1m;
                }

                ListaUnidadesPrecios.Add(new UnidadMedidaPrecioItem
                {
                    NombreUnidad = u,
                    EstaSeleccionada = seleccionada,
                    PrecioTexto = precio > 0 ? precio.ToString("0.00") : "10.00",
                    FactorTexto = factor.ToString("0.##")
                });
            }
        }

        private void EjecutarAgregarNuevaUnidad()
        {
            if (string.IsNullOrWhiteSpace(NuevaUnidadNombre)) return;

            string nombre = NuevaUnidadNombre.Trim();
            var existe = ListaUnidadesPrecios.FirstOrDefault(u => string.Equals(u.NombreUnidad, nombre, StringComparison.OrdinalIgnoreCase));
            if (existe != null)
            {
                existe.EstaSeleccionada = true;
            }
            else
            {
                decimal.TryParse(FormPrecio.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precioBase);
                ListaUnidadesPrecios.Add(new UnidadMedidaPrecioItem
                {
                    NombreUnidad = nombre,
                    EstaSeleccionada = true,
                    PrecioTexto = precioBase > 0 ? precioBase.ToString("0.00") : "10.00"
                });
            }

            NuevaUnidadNombre = string.Empty;
        }

        private void CargarDatosEnFormulario(Producto p)
        {
            FormId = p.Id;
            FormCodigo = p.Codigo;
            FormNombre = p.Nombre;
            FormCategoria = p.Categoria;
            FormPrecio = p.PrecioUnitario.ToString("0.00");
            FormStockActual = p.StockActual.ToString("0.##");
            FormStockMinimo = p.StockMinimo.ToString("0.##");
            FormCuMax = p.CuMax.ToString("0.##");
            FormUnidad = p.UnidadMedida;
            FormUnidadesPermitidas = p.UnidadesPermitidas ?? string.Empty;
            EsModoEdicion = true;

            InicializarListaUnidadesPrecios(p);
        }

        private void LimpiarFormulario()
        {
            FormId = 0;
            FormCodigo = string.Empty;
            FormNombre = string.Empty;
            FormCategoria = "Construcción";
            FormPrecio = "10.00";
            FormStockActual = "10";
            FormStockMinimo = "5";
            FormCuMax = "50";
            FormUnidad = "Unidad";
            FormUnidadesPermitidas = string.Empty;
            EsModoEdicion = false;
            ProductoSeleccionado = null;

            InicializarListaUnidadesPrecios(null);
        }

        private void EjecutarEditarProducto(object? param)
        {
            if (param is Producto p)
            {
                CargarDatosEnFormulario(p);
            }
        }

        private void EjecutarGuardarProducto()
        {
            if (string.IsNullOrWhiteSpace(FormCodigo) || string.IsNullOrWhiteSpace(FormNombre))
            {
                MessageBox.Show("El código y el nombre del producto son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(FormPrecio.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precioBase) || precioBase <= 0)
            {
                MessageBox.Show("Ingrese un precio unitario válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(FormStockActual.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal stock);
            decimal.TryParse(FormStockMinimo.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal stockMin);
            decimal.TryParse(FormCuMax.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cuMax);

            if (cuMax < stock)
            {
                MessageBox.Show("La capacidad máxima (CuMax) no puede ser inferior al stock actual.", "Validación de Almacén", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Recopilar unidades seleccionadas y sus precios específicos de las casillas marcadas
            var unidadesSeleccionadas = ListaUnidadesPrecios
                .Where(u => u.EstaSeleccionada)
                .Select(u => u.NombreUnidad.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unidadesSeleccionadas.Count == 0)
            {
                unidadesSeleccionadas.Add("Unidad");
                var itemUnidad = ListaUnidadesPrecios.FirstOrDefault(u => string.Equals(u.NombreUnidad, "Unidad", StringComparison.OrdinalIgnoreCase));
                if (itemUnidad != null) itemUnidad.EstaSeleccionada = true;
            }

            string unidadPrincipal = unidadesSeleccionadas[0];

            var dictPrecios = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in ListaUnidadesPrecios.Where(u => u.EstaSeleccionada))
            {
                decimal p = item.ObtenerPrecio();
                dictPrecios[item.NombreUnidad.Trim()] = p > 0 ? p : precioBase;
            }

            if (!dictPrecios.ContainsKey(unidadPrincipal))
            {
                dictPrecios[unidadPrincipal] = precioBase;
            }

            decimal precioPrincipal = dictPrecios[unidadPrincipal];

            var producto = new Producto
            {
                Id = FormId,
                Codigo = FormCodigo.Trim(),
                Nombre = FormNombre.Trim(),
                Categoria = string.IsNullOrWhiteSpace(FormCategoria) ? "General" : FormCategoria.Trim(),
                PrecioUnitario = precioPrincipal,
                StockActual = stock,
                StockMinimo = stockMin,
                CuMax = cuMax,
                UnidadMedida = unidadPrincipal
            };

            var dictFactores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in ListaUnidadesPrecios.Where(u => u.EstaSeleccionada))
            {
                decimal f = item.ObtenerFactor();
                dictFactores[item.NombreUnidad.Trim()] = f > 0 ? f : 1.0m;
            }

            producto.SetUnidadesPermitidas(unidadesSeleccionadas);
            producto.GuardarPreciosPorUnidad(dictPrecios);
            producto.GuardarFactoresPorUnidad(dictFactores);

            bool exito;
            string error;

            if (FormId == 0)
            {
                exito = _inventoryService.CrearProducto(producto, out error);
            }
            else
            {
                exito = _inventoryService.ActualizarProducto(producto, out error);
            }

            if (!exito)
            {
                MessageBox.Show(error, "Error al Guardar Producto", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"¡Producto '{producto.Nombre}' guardado correctamente con sus precios por unidad!", "Operación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            LimpiarFormulario();
            CargarProductos();
        }

        private void EjecutarEliminarProducto(object? param)
        {
            var prod = param as Producto ?? ProductoSeleccionado;
            if (prod == null)
            {
                MessageBox.Show("Seleccione un producto para eliminar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmacion = MessageBox.Show($"¿Está seguro de eliminar el producto '{prod.Nombre}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmacion != MessageBoxResult.Yes) return;

            if (!_inventoryService.EliminarProducto(prod.Id, out string error))
            {
                MessageBox.Show(error, "No se pudo eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Producto eliminado con éxito.", "Eliminado", MessageBoxButton.OK, MessageBoxImage.Information);
            LimpiarFormulario();
            CargarProductos();
        }

        private void EjecutarAumentoMasivo()
        {
            if (!decimal.TryParse(PorcentajeAumentoTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal porcentaje) || porcentaje == 0)
            {
                MessageBox.Show("Ingrese un porcentaje válido distinto de 0% (ej: 5.0 para +5%, -3.0 para -3%).", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string catFiltro = CategoriaAumento == "Todas" ? "TODAS las categorías" : $"la categoría '{CategoriaAumento}'";
            var confirmacion = MessageBox.Show($"¿Desea aplicar un cambio de precios del {porcentaje:0.#}% a {catFiltro}?", "Confirmación de Actualización Masiva", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmacion != MessageBoxResult.Yes) return;

            int modificados = _inventoryService.ActualizarPreciosMasivamente(CategoriaAumento, porcentaje, out string mensaje);
            MessageBox.Show(mensaje, "Actualización Masiva Concluida", MessageBoxButton.OK, MessageBoxImage.Information);

            CargarProductos();
        }
    }
}
