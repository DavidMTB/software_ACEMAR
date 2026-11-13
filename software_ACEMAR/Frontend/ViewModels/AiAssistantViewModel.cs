using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using software_ACEMAR.Services;

namespace software_ACEMAR.ViewModels
{
    public class ChatMessageItem
    {
        public string Emisor { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public bool EsIa { get; set; }
        public string Icono => EsIa ? "🧠" : "👤";
        public string HoraFormateada => FechaHora.ToString("HH:mm");
        public string BackgroundColor => EsIa ? "#FFFFFF" : "#4A5D23";
        public string TextColor => EsIa ? "#1E2514" : "#FFFFFF";
        public string BorderColor => EsIa ? "#DCE3D6" : "#354318";
        public HorizontalAlignment Alineacion => EsIa ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    public class AiAssistantViewModel : ViewModelBase
    {
        private readonly IAiAssistantService _aiService;

        private string _mensajeInput = string.Empty;
        private bool _estaCargando;
        private string _textoEstado = "Listo para consultar con el Asistente IA (Gemini)";
        private string _apiKeyInput = string.Empty;
        private bool _mostrarConfiguracion;
        private string _estadoConexion = string.Empty;

        public ObservableCollection<ChatMessageItem> Mensajes { get; } = new();

        public ICommand EnviarMensajeCommand { get; }
        public ICommand GenerarDiagnosticoGeneralCommand { get; }
        public ICommand GenerarDiagnosticoStockCommand { get; }
        public ICommand GenerarDiagnosticoCreditosCommand { get; }
        public ICommand GenerarEstrategiaComercialCommand { get; }
        public ICommand AlternarConfiguracionCommand { get; }
        public ICommand GuardarApiKeyCommand { get; }
        public ICommand LimpiarChatCommand { get; }
        public ICommand CopiarMensajeCommand { get; }

        public AiAssistantViewModel(IAiAssistantService? aiService = null)
        {
            _aiService = aiService ?? new AiAssistantService();

            EnviarMensajeCommand = new RelayCommand(async _ => await EjecutarEnviarMensajeAsync(), _ => !EstaCargando && !string.IsNullOrWhiteSpace(MensajeInput));
            GenerarDiagnosticoGeneralCommand = new RelayCommand(async _ => await EjecutarDiagnosticoAsync("General"), _ => !EstaCargando);
            GenerarDiagnosticoStockCommand = new RelayCommand(async _ => await EjecutarDiagnosticoAsync("Stock"), _ => !EstaCargando);
            GenerarDiagnosticoCreditosCommand = new RelayCommand(async _ => await EjecutarDiagnosticoAsync("Creditos"), _ => !EstaCargando);
            GenerarEstrategiaComercialCommand = new RelayCommand(async _ => await EjecutarDiagnosticoAsync("Comercial"), _ => !EstaCargando);

            AlternarConfiguracionCommand = new RelayCommand(_ => MostrarConfiguracion = !MostrarConfiguracion);
            GuardarApiKeyCommand = new RelayCommand(_ => EjecutarGuardarApiKey());
            LimpiarChatCommand = new RelayCommand(_ => LimpiarChat());
            CopiarMensajeCommand = new RelayCommand(param => EjecutarCopiarMensaje(param?.ToString()));

            ApiKeyInput = _aiService.ObtenerApiKey();
            ActualizarEstadoConexion();

            Mensajes.Add(new ChatMessageItem
            {
                Emisor = "ACEMAR IA (Gemini)",
                EsIa = true,
                Contenido = "¡Hola! Soy tu **Asistente Ejecutivo con Inteligencia Artificial** de Ferretería ACEMAR (potenciado por Google Gemini).\n\n" +
                            "Estoy conectado en tiempo real a tu base de datos de productos, lotes FIFO, ventas y fiados. " +
                            "Puedo generar diagnósticos gerenciales automáticos, detectar materiales en stock crítico, analizar tus cuentas por cobrar o responder cualquier duda comercial.\n\n" +
                            "👉 *Selecciona una de las acciones rápidas arriba o escribe tu pregunta abajo para comenzar.*",
                FechaHora = DateTime.Now
            });
        }

        public string MensajeInput
        {
            get => _mensajeInput;
            set
            {
                if (SetField(ref _mensajeInput, value))
                {
                    (EnviarMensajeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool EstaCargando
        {
            get => _estaCargando;
            set
            {
                if (SetField(ref _estaCargando, value))
                {
                    (EnviarMensajeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (GenerarDiagnosticoGeneralCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (GenerarDiagnosticoStockCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (GenerarDiagnosticoCreditosCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (GenerarEstrategiaComercialCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string TextoEstado
        {
            get => _textoEstado;
            set => SetField(ref _textoEstado, value);
        }

        public string ApiKeyInput
        {
            get => _apiKeyInput;
            set => SetField(ref _apiKeyInput, value);
        }

        public bool MostrarConfiguracion
        {
            get => _mostrarConfiguracion;
            set => SetField(ref _mostrarConfiguracion, value);
        }

        public string EstadoConexion
        {
            get => _estadoConexion;
            set => SetField(ref _estadoConexion, value);
        }

        private void ActualizarEstadoConexion()
        {
            if (_aiService.TieneApiKeyConfigurada())
            {
                EstadoConexion = "🟢 Conectado con Google Gemini API (Modo Nube)";
            }
            else
            {
                EstadoConexion = "🟡 Modo Local / Heurístico (Sin API Key configurada)";
            }
        }

        private void EjecutarGuardarApiKey()
        {
            _aiService.GuardarApiKey(ApiKeyInput);
            ActualizarEstadoConexion();
            MostrarConfiguracion = false;
            MessageBox.Show("Clave API de Gemini guardada correctamente.", "Configuración de IA", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task EjecutarEnviarMensajeAsync()
        {
            string texto = MensajeInput.Trim();
            if (string.IsNullOrWhiteSpace(texto) || EstaCargando) return;

            Mensajes.Add(new ChatMessageItem
            {
                Emisor = "Gerente",
                EsIa = false,
                Contenido = texto,
                FechaHora = DateTime.Now
            });

            MensajeInput = string.Empty;
            EstaCargando = true;
            TextoEstado = "Consultando con la Inteligencia Artificial...";

            try
            {
                var historial = Mensajes
                    .Where(m => m.Contenido != texto)
                    .Select(m => (m.Emisor, m.Contenido))
                    .ToList();

                string respuesta = await _aiService.ConsultarAsistenteAsync(texto, historial);

                Mensajes.Add(new ChatMessageItem
                {
                    Emisor = "ACEMAR IA (Gemini)",
                    EsIa = true,
                    Contenido = respuesta,
                    FechaHora = DateTime.Now
                });

                TextoEstado = "Respuesta generada exitosamente con Gemini.";
            }
            catch (Exception ex)
            {
                Mensajes.Add(new ChatMessageItem
                {
                    Emisor = "ACEMAR IA (Gemini)",
                    EsIa = true,
                    Contenido = $"⚠️ Ocurrió un error al procesar tu consulta: {ex.Message}",
                    FechaHora = DateTime.Now
                });
                TextoEstado = "Error en la consulta.";
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task EjecutarDiagnosticoAsync(string tipo)
        {
            if (EstaCargando) return;

            string tipoEtiqueta = tipo switch
            {
                "Stock" => "Auditoría de Stock & Proveedores",
                "Creditos" => "Evaluación de Riesgo de Créditos y Fiados",
                "Comercial" => "Estrategias Comerciales y Precios",
                _ => "Diagnóstico Ejecutivo Integral de Negocio"
            };

            Mensajes.Add(new ChatMessageItem
            {
                Emisor = "Gerente",
                EsIa = false,
                Contenido = $"Solicitud de informe: {tipoEtiqueta}",
                FechaHora = DateTime.Now
            });

            EstaCargando = true;
            TextoEstado = $"Analizando datos y redactando {tipoEtiqueta} con Gemini...";

            try
            {
                string respuesta = await _aiService.GenerarDiagnosticoEjecutivoAsync(tipo);

                Mensajes.Add(new ChatMessageItem
                {
                    Emisor = "ACEMAR IA (Gemini)",
                    EsIa = true,
                    Contenido = respuesta,
                    FechaHora = DateTime.Now
                });

                TextoEstado = "Diagnóstico generado correctamente con Gemini.";
            }
            catch (Exception ex)
            {
                Mensajes.Add(new ChatMessageItem
                {
                    Emisor = "ACEMAR IA (Gemini)",
                    EsIa = true,
                    Contenido = $"⚠️ No se pudo completar el diagnóstico: {ex.Message}",
                    FechaHora = DateTime.Now
                });
                TextoEstado = "Error al generar informe.";
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private void LimpiarChat()
        {
            Mensajes.Clear();
            Mensajes.Add(new ChatMessageItem
            {
                Emisor = "ACEMAR Copilot",
                EsIa = true,
                Contenido = "Historial reiniciado. ¿En qué aspecto del negocio deseas enfocar el análisis ahora?",
                FechaHora = DateTime.Now
            });
            TextoEstado = "Listo para consultar.";
        }

        private void EjecutarCopiarMensaje(string? contenido)
        {
            if (!string.IsNullOrWhiteSpace(contenido))
            {
                Clipboard.SetText(contenido);
                MessageBox.Show("Texto copiado al portapapeles.", "Copiar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
