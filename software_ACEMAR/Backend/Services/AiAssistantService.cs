using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IAiAssistantService
    {
        string ObtenerApiKey();
        void GuardarApiKey(string apiKey);
        bool TieneApiKeyConfigurada();

        Task<string> GenerarDiagnosticoEjecutivoAsync(string tipoDiagnostico = "General", CancellationToken cancellationToken = default);
        Task<string> ConsultarAsistenteAsync(string preguntaUsuario, IEnumerable<(string Rol, string Contenido)>? historial = null, CancellationToken cancellationToken = default);
    }

    public class AiConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Modelo { get; set; } = "gemini-flash-lite-latest";
    }

    public class ProductoInfoContexto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal PrecioUnitario { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public string ProveedorHabitual { get; set; } = string.Empty;
    }

    public class VentaInfoContexto
    {
        public string NumeroComprobante { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string TipoVenta { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new();
    }

    public class ProveedorInfoContexto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Contacto { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string WhatsApp { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string LineasSuministro { get; set; } = string.Empty;
    }

    public class DatosContextoNegocio
    {
        public int TotalProductos { get; set; }
        public int ProductosStockBajo { get; set; }
        public List<string> ListaProductosStockCritico { get; set; } = new();
        public decimal TotalVentasSemana { get; set; }
        public int CantidadTicketsSemana { get; set; }
        public decimal TicketPromedioSemana { get; set; }
        public decimal VentasEfectivoSemana { get; set; }
        public decimal VentasDigitalSemana { get; set; }
        public decimal VentasCreditoSemana { get; set; }
        public List<string> TopProductosVendidos { get; set; } = new();
        public decimal DeudaTotalClientes { get; set; }
        public int ClientesConDeuda { get; set; }
        public List<string> ClientesMayorDeuda { get; set; } = new();
        public int LotesActivos { get; set; }
        public int LotesPorAgotarse { get; set; }

        public List<ProductoInfoContexto> CatalogoProductos { get; set; } = new();
        public List<VentaInfoContexto> HistorialVentasRecientes { get; set; } = new();
        public List<ProveedorInfoContexto> DirectorioProveedores { get; set; } = new();
        public string EstadoCajaResumen { get; set; } = string.Empty;
    }

    public class AiAssistantService : IAiAssistantService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly string[] ModelosCandidatos = new[] 
        { 
            "gemini-flash-lite-latest", 
            "gemini-2.5-flash-lite", 
            "gemini-3.5-flash-lite",
            "gemini-flash-latest", 
            "gemini-3.8-flash",
            "gemini-pro-latest" 
        };

        private readonly string _configFilePath;
        private AiConfig _config;

        public AiAssistantService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_settings.json");
            _config = CargarConfiguracion();
        }

        private AiConfig CargarConfiguracion()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                    var cfg = JsonSerializer.Deserialize<AiConfig>(json);
                    if (cfg != null)
                    {

                        if (string.IsNullOrWhiteSpace(cfg.Modelo) || cfg.Modelo.Contains("1.5") || cfg.Modelo == "gemini-2.5-flash")
                        {
                            cfg.Modelo = "gemini-flash-lite-latest";
                            GuardarConfiguracionEnDisco(cfg);
                        }
                        return cfg;
                    }
                }
            }
            catch { }

            string? envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            var nuevaConfig = new AiConfig { ApiKey = envKey ?? string.Empty, Modelo = "gemini-flash-lite-latest" };
            return nuevaConfig;
        }

        private void GuardarConfiguracionEnDisco(AiConfig cfg)
        {
            try
            {
                string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al escribir ai_settings.json: {ex.Message}");
            }
        }

        public string ObtenerApiKey() => _config.ApiKey ?? string.Empty;

        public void GuardarApiKey(string apiKey)
        {
            _config.ApiKey = apiKey.Trim();
            if (string.IsNullOrWhiteSpace(_config.Modelo) || _config.Modelo.Contains("1.5") || _config.Modelo == "gemini-2.5-flash")
            {
                _config.Modelo = "gemini-flash-lite-latest";
            }
            GuardarConfiguracionEnDisco(_config);
        }

        public bool TieneApiKeyConfigurada() => !string.IsNullOrWhiteSpace(_config.ApiKey);

        public async Task<string> GenerarDiagnosticoEjecutivoAsync(string tipoDiagnostico = "General", CancellationToken cancellationToken = default)
        {
            var datos = await RecopilarDatosNegocioAsync(cancellationToken);

            string prompt = tipoDiagnostico switch
            {
                "Stock" => "Realiza una auditoría y recomendaciones de reposición de stock a proveedores. Identifica riesgos de quiebre de stock y qué productos urgentes deben pedirse.",
                "Creditos" => "Realiza un análisis de la cartera de clientes de crédito (fiados). Evalúa el riesgo de morosidad, concentración de deuda y acciones sugeridas de cobranza.",
                "Comercial" => "Propón 3 estrategias comerciales y promociones para elevar las ventas y la rotación de materiales, considerando los márgenes y productos más vendidos.",
                _ => "Realiza un Diagnóstico Ejecutivo Integral de la Ferretería ACEMAR. Incluye salud de ventas, alertas prioritarias de stock, situación financiera de créditos y 3 recomendaciones clave para la gerencia."
            };

            return await ProcesarConsultaConContextoAsync(prompt, datos, null, cancellationToken);
        }

        public async Task<string> ConsultarAsistenteAsync(string preguntaUsuario, IEnumerable<(string Rol, string Contenido)>? historial = null, CancellationToken cancellationToken = default)
        {
            var datos = await RecopilarDatosNegocioAsync(cancellationToken);
            return await ProcesarConsultaConContextoAsync(preguntaUsuario, datos, historial, cancellationToken);
        }

        private async Task<string> ProcesarConsultaConContextoAsync(
            string solicitud,
            DatosContextoNegocio datos,
            IEnumerable<(string Rol, string Contenido)>? historial,
            CancellationToken cancellationToken)
        {

            if (!TieneApiKeyConfigurada())
            {
                return await GenerarRespuestaHeuristicaLocalAsync(solicitud, datos, "⚠️ MODO LOCAL / DEMO (Sin API Key configurada)\nGenerado con el motor interno de datos de SQLite:");
            }

            try
            {
                string respuestaGemini = await LlamarGeminiApiAsync(solicitud, datos, historial, cancellationToken);
                return respuestaGemini;
            }
            catch (Exception ex)
            {

                return await GenerarRespuestaHeuristicaLocalAsync(solicitud, datos, $"⚠️ MODO DE RESPALDO (Error al conectar con la API de Gemini: {ex.Message})\nEl sistema ha procesado tu consulta con el motor analítico local:");
            }
        }

        private async Task<string> LlamarGeminiApiAsync(
            string solicitud,
            DatosContextoNegocio datos,
            IEnumerable<(string Rol, string Contenido)>? historial,
            CancellationToken cancellationToken)
        {
            string apiKey = _config.ApiKey.Trim();

            var sbContexto = new StringBuilder();
            sbContexto.AppendLine("================================================================================");
            sbContexto.AppendLine("BASE DE DATOS VIVA DEL SISTEMA - FERRETERÍA & ALMACÉN ACEMAR S.A.C.");
            sbContexto.AppendLine("================================================================================");
            sbContexto.AppendLine();

            sbContexto.AppendLine("--- 1. RESUMEN FINANCIERO Y COMERCIAL SEMANAL ---");
            sbContexto.AppendLine($"- Total productos en catálogo: {datos.TotalProductos}");
            sbContexto.AppendLine($"- Ventas últimos 7 días: S/ {datos.TotalVentasSemana:N2} en {datos.CantidadTicketsSemana} comprobantes (Ticket promedio: S/ {datos.TicketPromedioSemana:N2})");
            sbContexto.AppendLine($"  * Efectivo: S/ {datos.VentasEfectivoSemana:N2} | Digitales: S/ {datos.VentasDigitalSemana:N2} | Crédito: S/ {datos.VentasCreditoSemana:N2}");
            if (!string.IsNullOrWhiteSpace(datos.EstadoCajaResumen))
            {
                sbContexto.AppendLine($"- Estado de Caja Registradora: {datos.EstadoCajaResumen}");
            }
            sbContexto.AppendLine();

            sbContexto.AppendLine("--- 2. HISTORIAL DE VENTAS RECIENTES (ORDEN CRONOLÓGICO DECRECIENTE) ---");
            if (datos.HistorialVentasRecientes.Any())
            {
                var ultVenta = datos.HistorialVentasRecientes.First();
                sbContexto.AppendLine($"* ÚLTIMA VENTA REALIZADA: Comprobante {ultVenta.NumeroComprobante} el {ultVenta.Fecha:dd/MM/yyyy HH:mm:ss} | Cliente: {ultVenta.Cliente} | Pago: {ultVenta.TipoVenta} | TOTAL: S/ {ultVenta.Total:N2}");
                sbContexto.AppendLine($"  Detalle de ítems vendidos: {string.Join("; ", ultVenta.Items)}");
                sbContexto.AppendLine();

                sbContexto.AppendLine("* Lista de comprobantes recientes:");
                foreach (var vta in datos.HistorialVentasRecientes.Take(20))
                {
                    sbContexto.AppendLine($"  - [{vta.NumeroComprobante}] {vta.Fecha:dd/MM/yyyy HH:mm} | Total: S/ {vta.Total:N2} ({vta.TipoVenta}) | Cliente: {vta.Cliente} | Productos: {string.Join(", ", vta.Items)}");
                }
            }
            else
            {
                sbContexto.AppendLine("No hay ventas registradas aún en el sistema.");
            }
            sbContexto.AppendLine();

            sbContexto.AppendLine("--- 3. CATÁLOGO COMPLETO DE PRODUCTOS EN BASE DE DATOS (STOCK Y PRECIOS REALES) ---");
            foreach (var p in datos.CatalogoProductos)
            {
                string alerta = (p.StockMinimo > 0 && p.StockActual <= p.StockMinimo) ? " [ALERTA STOCK BAJO]" : "";
                sbContexto.AppendLine($"[{p.Codigo}] {p.Nombre} | Categoría: {p.Categoria} | Stock Actual: {p.StockActual:0.##} {p.UnidadMedida} (Mínimo: {p.StockMinimo:0.##}) | Precio Venta: S/ {p.PrecioUnitario:N2} | Proveedor: {p.ProveedorHabitual}{alerta}");
            }
            sbContexto.AppendLine();

            sbContexto.AppendLine("--- 4. DIRECTORIO DE PROVEEDORES ASOCIADOS Y LÍNEAS DE SUMINISTRO ---");
            foreach (var pr in datos.DirectorioProveedores)
            {
                sbContexto.AppendLine($"• {pr.Nombre} | Contacto: {pr.Contacto} | Tel: {pr.Telefono} / WS: {pr.WhatsApp} | Correo: {pr.Correo} | Dirección: {pr.Direccion} | Suministro: {pr.LineasSuministro}");
            }
            sbContexto.AppendLine();

            sbContexto.AppendLine("--- 5. CARTERA DE CLIENTES DE CRÉDITO Y FIADOS PENDIENTES ---");
            sbContexto.AppendLine($"- Deuda total activa por cobrar: S/ {datos.DeudaTotalClientes:N2} en {datos.ClientesConDeuda} clientes con saldo deudor.");
            foreach (var c in datos.ClientesMayorDeuda)
            {
                sbContexto.AppendLine($"  - 👤 {c}");
            }
            sbContexto.AppendLine("================================================================================");

            string systemInstruction = 
                "Eres el Asistente Ejecutivo de Inteligencia Artificial para la Gerencia de 'Ferretería & Almacén ACEMAR S.A.C.' (potenciado por Google Gemini).\n\n" +
                "TU ALCANCE Y ESPECIALIDAD:\n" +
                "- Tu función es asistir al gerente y vendedores en la toma de decisiones, control de almacén FIFO, auditorías de stock, historial de ventas, cartera de créditos y compras a proveedores.\n" +
                "- Toda tu atención debe estar CENTRADA EXCLUSIVAMENTE en Ferretería ACEMAR y en los datos suministrados.\n\n" +
                "REGLAS CRÍTICAS DE RESPUESTA (GROUNDING Y VERACIDAD ESTRICTA):\n" +
                "1. NUNCA INVENTES DATOS NI PRODUCTOS:\n" +
                "   - Tu conocimiento de inventario, ventas, clientes y proveedores proviene ÚNICAMENTE de la base de datos viva incluida en este prompt.\n" +
                "   - NUNCA inventes productos, marcas, códigos, precios ni ventas inexistentes.\n" +
                "   - Si te preguntan por un producto que no está en el catálogo, indica con precisión: \"El producto '[nombre]' no está registrado en el catálogo de Ferretería ACEMAR.\"\n" +
                "   - Si te piden \"todos los tipos de arena que vendemos\", busca en el catálogo todos los productos que contengan 'arena' (ej: Arena Gruesa Lavada [ARE-003], Arena Fina Cantera [ARE-004]) y presenta sus códigos, stock disponible, unidades y precios sin inventar ningún otro.\n\n" +
                "2. CONSULTAS SOBRE VENTAS RECIENTES:\n" +
                "   - Si te preguntan \"¿cuál fue la última venta?\" o \"lista de las últimas ventas\", NUNCA digas que no puedes verlas. Revisa la sección 'HISTORIAL DE VENTAS RECIENTES' y detalla el número de comprobante, fecha, hora, cliente, forma de pago, monto total en soles (S/) y los productos vendidos.\n\n" +
                "3. PROVEEDORES Y REPOSICIÓN:\n" +
                "   - Si consultan a quién se le compra un producto o datos de contacto de proveedores, revisa la sección 'DIRECTORIO DE PROVEEDORES ASOCIADOS' y la columna 'Proveedor' del catálogo.\n\n" +
                "4. FORMATO PROFESIONAL:\n" +
                "   - Responde siempre con profesionalismo, claridad y viñetas estructuradas en Markdown.\n" +
                "   - Utiliza la moneda nacional de Perú (S/ Soles) y las unidades de medida reales (Bolsa, Metros, Kg, Unidad, Ciento, Par, etc.).";

            var contents = new List<object>();

            if (historial != null)
            {
                foreach (var (rol, contenido) in historial.TakeLast(6))
                {
                    string roleGemini = rol.Equals("Usuario", StringComparison.OrdinalIgnoreCase) ? "user" : "model";
                    contents.Add(new
                    {
                        role = roleGemini,
                        parts = new[] { new { text = contenido } }
                    });
                }
            }

            string mensajeFinal = $"{systemInstruction}\n\n{sbContexto}\n\nSOLICITUD O PREGUNTA DEL GERENTE:\n{solicitud}";
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = mensajeFinal } }
            });

            var requestBody = new
            {
                contents = contents,
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 2048
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);

            var modelosAIntentar = new List<string>();
            string modeloConfigurado = string.IsNullOrWhiteSpace(_config.Modelo) || _config.Modelo.Contains("1.5") || _config.Modelo == "gemini-2.5-flash"
                ? "gemini-flash-lite-latest" 
                : _config.Modelo.Trim();
            modelosAIntentar.Add(modeloConfigurado);
            foreach (var cand in ModelosCandidatos)
            {
                if (!modelosAIntentar.Contains(cand)) modelosAIntentar.Add(cand);
            }

            Exception? ultimaExcepcion = null;

            foreach (var modelo in modelosAIntentar)
            {
                try
                {
                    string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent?key={apiKey}";
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    using var response = await _httpClient.PostAsync(url, content, cts.Token);
                    string responseString = await response.Content.ReadAsStringAsync(cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseString);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var primerCandidate = candidates[0];
                            if (primerCandidate.TryGetProperty("content", out var contentElem) &&
                                contentElem.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                string? textoGenerado = parts[0].GetProperty("text").GetString();
                                if (!string.IsNullOrWhiteSpace(textoGenerado))
                                {

                                    if (_config.Modelo != modelo)
                                    {
                                        _config.Modelo = modelo;
                                        GuardarConfiguracionEnDisco(_config);
                                    }
                                    return textoGenerado.Trim();
                                }
                            }
                        }
                    }
                    else if ((int)response.StatusCode == 404 || (int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                    {

                        continue;
                    }
                    else
                    {
                        throw new Exception($"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}: {responseString}");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    ultimaExcepcion = ex;
                }
            }

            throw ultimaExcepcion ?? new Exception("No se pudo conectar con ningún modelo de Gemini disponible.");
        }

        private async Task<string> GenerarRespuestaHeuristicaLocalAsync(string solicitud, DatosContextoNegocio datos, string encabezadoAviso)
        {
            await Task.Yield();
            string s = solicitud.Trim().ToLowerInvariant();

            if (s == "hola" || s == "buenos dias" || s == "buenas tardes" || s == "saludos" || s.StartsWith("hola"))
            {
                return $"{encabezadoAviso}\n\n" +
                       "👋 ¡Hola! Soy el **Asistente de IA (Gemini)** de Ferretería ACEMAR.\n\n" +
                       "Puedo ayudarte con:\n" +
                       "• **Ventas recientes** (ej: *'¿Cuál fue la última venta?'*, *'Lista de las últimas ventas'*).\n" +
                       "• **Consultar stock, precios y marcas** (ej: *'¿Dime todos los tipos de arena que vendemos?'*, *'Stock de cemento'*).\n" +
                       "• **Proveedores y reposición** (ej: *'¿Quién es nuestro proveedor de fierro?'*, *'Proveedores registrados'*).\n" +
                       "• **Estado de deudas y fiados** (ej: *'¿Quiénes son los clientes que más deben?'*).\n" +
                       "• **Diagnósticos gerenciales** (puedes usar los botones de arriba para auditorías inmediatas).\n\n" +
                       "¿Qué consulta o reporte necesitas revisar hoy?";
            }

            bool esPeticionVentas = s.Contains("ultima venta") || s.Contains("última venta") || 
                                    s.Contains("ultimas ventas") || s.Contains("últimas ventas") || 
                                    s.Contains("lista de venta") || s.Contains("historial de venta") ||
                                    (s.Contains("venta") && (s.Contains("reciente") || s.Contains("ayer") || s.Contains("hoy") || s.Contains("comprobante") || s.Contains("boleta") || s.Contains("ticket") || s.Contains("lista")));

            if (esPeticionVentas && datos.HistorialVentasRecientes.Any())
            {
                var sbVentas = new StringBuilder();
                sbVentas.AppendLine(encabezadoAviso);
                sbVentas.AppendLine();

                bool esSoloUnaVenta = (s.Contains("la ultima") || s.Contains("la última") || s.Contains("cual fue") || s.Contains("cuál fue") || s == "ultima venta" || s == "última venta") && !s.Contains("lista") && !s.Contains("todas");

                if (esSoloUnaVenta)
                {
                    var ult = datos.HistorialVentasRecientes.First();
                    sbVentas.AppendLine("🧾 **INFORMACIÓN DE LA ÚLTIMA VENTA REGISTRADA:**");
                    sbVentas.AppendLine("─────────────────────────────────────────────────────────────────");
                    sbVentas.AppendLine($"• **Comprobante:** #{ult.NumeroComprobante}");
                    sbVentas.AppendLine($"• **Fecha y Hora:** {ult.Fecha:dd/MM/yyyy HH:mm:ss}");
                    sbVentas.AppendLine($"• **Cliente:** {ult.Cliente}");
                    sbVentas.AppendLine($"• **Modalidad de Pago:** {ult.TipoVenta}");
                    sbVentas.AppendLine($"• **TOTAL VENTA:** **S/ {ult.Total:N2}**");
                    sbVentas.AppendLine();
                    sbVentas.AppendLine("📦 **Detalle de Materiales Vendidos:**");
                    foreach (var it in ult.Items)
                    {
                        sbVentas.AppendLine($"  - {it}");
                    }
                    return sbVentas.ToString();
                }

                sbVentas.AppendLine($"📋 **HISTORIAL DE ÚLTIMAS VENTAS REGISTRADAS ({Math.Min(10, datos.HistorialVentasRecientes.Count)} más recientes):**");
                sbVentas.AppendLine("─────────────────────────────────────────────────────────────────");
                foreach (var vta in datos.HistorialVentasRecientes.Take(10))
                {
                    sbVentas.AppendLine($"• **[{vta.NumeroComprobante}]** {vta.Fecha:dd/MM/yyyy HH:mm} | **S/ {vta.Total:N2}** ({vta.TipoVenta})");
                    sbVentas.AppendLine($"  👤 Cliente: {vta.Cliente}");
                    if (vta.Items.Any())
                    {
                        sbVentas.AppendLine($"  📦 Ítems: {string.Join(", ", vta.Items.Take(3))}{(vta.Items.Count > 3 ? "..." : "")}");
                    }
                    sbVentas.AppendLine();
                }
                return sbVentas.ToString();
            }

            bool esPeticionProveedores = s.Contains("proveedor") || s.Contains("proveedores") || 
                                          s.Contains("a quien compramos") || s.Contains("quien nos vende") || 
                                          s.Contains("contacto de") || s.Contains("telefono de");

            if (esPeticionProveedores && datos.DirectorioProveedores.Any())
            {
                var sbProv = new StringBuilder();
                sbProv.AppendLine(encabezadoAviso);
                sbProv.AppendLine();
                sbProv.AppendLine($"🏭 **DIRECTORIO DE PROVEEDORES DE FERRETERÍA ACEMAR ({datos.DirectorioProveedores.Count} registrados):**");
                sbProv.AppendLine("─────────────────────────────────────────────────────────────────");

                var provsFiltrados = datos.DirectorioProveedores
                    .Where(p => s.Contains(p.Nombre.ToLowerInvariant()) || 
                                s.Split(' ').Any(w => w.Length > 4 && (p.Nombre.ToLowerInvariant().Contains(w) || p.LineasSuministro.ToLowerInvariant().Contains(w))))
                    .ToList();

                var provsAMostrar = provsFiltrados.Any() ? provsFiltrados : datos.DirectorioProveedores.Take(8).ToList();

                foreach (var pr in provsAMostrar)
                {
                    sbProv.AppendLine($"• **{pr.Nombre}**");
                    sbProv.AppendLine($"  - Contacto: {pr.Contacto}");
                    if (!string.IsNullOrWhiteSpace(pr.Telefono) || !string.IsNullOrWhiteSpace(pr.WhatsApp))
                    {
                        sbProv.AppendLine($"  - Teléfono / WhatsApp: {pr.Telefono} / {pr.WhatsApp}");
                    }
                    if (!string.IsNullOrWhiteSpace(pr.Correo))
                    {
                        sbProv.AppendLine($"  - Correo: {pr.Correo}");
                    }
                    if (!string.IsNullOrWhiteSpace(pr.LineasSuministro))
                    {
                        sbProv.AppendLine($"  - Materiales: {pr.LineasSuministro}");
                    }
                    sbProv.AppendLine();
                }
                return sbProv.ToString();
            }

            if (s.Contains("caja") || s.Contains("arqueo") || s.Contains("efectivo en caja"))
            {
                var sbCaja = new StringBuilder();
                sbCaja.AppendLine(encabezadoAviso);
                sbCaja.AppendLine();
                sbCaja.AppendLine("💵 **ESTADO DE CAJA REGISTRADORA ACEMAR:**");
                sbCaja.AppendLine("─────────────────────────────────────────────────────────────────");
                sbCaja.AppendLine(!string.IsNullOrWhiteSpace(datos.EstadoCajaResumen) 
                    ? $"• {datos.EstadoCajaResumen}" 
                    : "No se encontró arqueo registrado recientemente.");
                return sbCaja.ToString();
            }

            bool esPeticionDiagnostico = s.Contains("diagnóstico") || s.Contains("diagnostico") || s.Contains("ejecutivo") || s.Contains("auditoría") || s.Contains("auditoria");

            if (!esPeticionDiagnostico)
            {
                var palabrasClave = s.Split(new[] { ' ', '?', '¿', ',', '.', ':', '!' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Where(w => w.Length >= 3 && w != "dime" && w != "todos" && w != "tipos" && w != "cuanto" && w != "queda" && w != "precio" && w != "para" && w != "como" && w != "donde" && w != "vendemos")
                                     .ToList();

                var coincidencias = datos.CatalogoProductos.Where(p =>
                    s.Contains(p.Nombre.ToLowerInvariant()) ||
                    s.Contains(p.Codigo.ToLowerInvariant()) ||
                    (!string.IsNullOrWhiteSpace(p.Categoria) && s.Contains(p.Categoria.ToLowerInvariant())) ||
                    palabrasClave.Any(k => p.Nombre.ToLowerInvariant().Contains(k) || p.Categoria.ToLowerInvariant().Contains(k) || p.Codigo.ToLowerInvariant().Contains(k))
                ).ToList();

                if (coincidencias.Any())
                {
                    var sbProd = new StringBuilder();
                    sbProd.AppendLine(encabezadoAviso);
                    sbProd.AppendLine();
                    sbProd.AppendLine($"📦 **CONSULTA DE PRODUCTOS Y STOCK EN ALMACÉN ({coincidencias.Count} resultado(s)):**");
                    sbProd.AppendLine("─────────────────────────────────────────────────────────────────");

                    foreach (var p in coincidencias)
                    {
                        string estadoAlerta = (p.StockMinimo > 0 && p.StockActual <= p.StockMinimo) 
                            ? "🚨 STOCK CRÍTICO (≤ Mínimo)" 
                            : "✅ Stock Disponible";

                        sbProd.AppendLine($"• **{p.Nombre}** [{p.Codigo}]");
                        sbProd.AppendLine($"  - Stock en Almacén: **{p.StockActual:0.##} {p.UnidadMedida}** ({estadoAlerta})");
                        sbProd.AppendLine($"  - Stock de Seguridad Mínimo: {p.StockMinimo:0.##} {p.UnidadMedida}");
                        sbProd.AppendLine($"  - Precio Unitario de Venta: **S/ {p.PrecioUnitario:N2}**");
                        sbProd.AppendLine($"  - Categoría: {p.Categoria}");
                        sbProd.AppendLine($"  - Proveedor Habitual: {p.ProveedorHabitual}");
                        sbProd.AppendLine();
                    }

                    return sbProd.ToString();
                }

                if (s.Contains("deuda") || s.Contains("fiado") || s.Contains("credito") || s.Contains("moros") || s.Contains("cobrar"))
                {
                    var sbCred = new StringBuilder();
                    sbCred.AppendLine(encabezadoAviso);
                    sbCred.AppendLine();
                    sbCred.AppendLine("💳 **ESTADO DE CARTERA DE CRÉDITOS Y FIADOS ACEMAR:**");
                    sbCred.AppendLine("─────────────────────────────────────────────────────────────────");
                    sbCred.AppendLine($"• **Deuda Total Activa:** S/ {datos.DeudaTotalClientes:N2}");
                    sbCred.AppendLine($"• **Clientes con Deuda Pendiente:** {datos.ClientesConDeuda} clientes");
                    sbCred.AppendLine();
                    if (datos.ClientesMayorDeuda.Any())
                    {
                        sbCred.AppendLine("• **Clientes con Mayor Saldo Deudor:**");
                        foreach (var c in datos.ClientesMayorDeuda.Take(5))
                        {
                            sbCred.AppendLine($"  - 👤 {c}");
                        }
                    }
                    sbCred.AppendLine();
                    sbCred.AppendLine("💡 **Recomendación:** Se sugiere no otorgar nuevas ventas a crédito a clientes que superen el 80% de su límite de crédito.");
                    return sbCred.ToString();
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(encabezadoAviso);
            sb.AppendLine();
            sb.AppendLine("📊 **REPORTE EJECUTIVO DE INTELIGENCIA DE NEGOCIO ACEMAR**");
            sb.AppendLine("═════════════════════════════════════════════════════════════════");
            sb.AppendLine($"• **Ventas Últimos 7 Días:** S/ {datos.TotalVentasSemana:N2} en {datos.CantidadTicketsSemana} comprobantes.");
            sb.AppendLine($"• **Ticket Promedio:** S/ {datos.TicketPromedioSemana:N2} por cliente.");
            sb.AppendLine($"• **Distribución de Pagos:** Efectivo (S/ {datos.VentasEfectivoSemana:N2}) | Digital (S/ {datos.VentasDigitalSemana:N2}) | Créditos (S/ {datos.VentasCreditoSemana:N2})");
            sb.AppendLine();

            sb.AppendLine("⚠️ **ESTADO DE INVENTARIO Y ALMACÉN (CONTROL FIFO)**");
            sb.AppendLine($"• Catálogo Total: {datos.TotalProductos} ítems registrados.");
            sb.AppendLine($"• Productos en Alerta Crítica (≤ Stock Mínimo): **{datos.ProductosStockBajo}**");
            if (datos.ListaProductosStockCritico.Any())
            {
                foreach (var item in datos.ListaProductosStockCritico.Take(4))
                {
                    sb.AppendLine($"  - 🚨 {item}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("💳 **CARTERA DE CRÉDITOS Y FIADOS (RIESGO)**");
            sb.AppendLine($"• Deuda Total Pendiente de Cobro: **S/ {datos.DeudaTotalClientes:N2}** ({datos.ClientesConDeuda} clientes con saldo).");
            if (datos.ClientesMayorDeuda.Any())
            {
                sb.AppendLine("• Clientes con Mayor Deuda Activa:");
                foreach (var c in datos.ClientesMayorDeuda.Take(3))
                {
                    sb.AppendLine($"  - 👤 {c}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("💡 **RECOMENDACIONES ESTRATÉGICAS SUGERIDAS:**");
            if (datos.ProductosStockBajo > 0)
            {
                sb.AppendLine("1. **Reposición Inmediata:** Emitir órdenes de compra a proveedores prioritarios para los materiales bajo stock de seguridad.");
            }
            else
            {
                sb.AppendLine("1. **Inventario Estable:** El almacén se encuentra abastecido por encima de los límites de contingencia.");
            }

            if (datos.DeudaTotalClientes > 500)
            {
                sb.AppendLine("2. **Gestión de Cobranza:** Priorizar la recuperación de fiados con los clientes de mayor deuda antes de otorgar nuevas líneas de crédito.");
            }
            else
            {
                sb.AppendLine("2. **Cartera Saludable:** El nivel de endeudamiento de clientes está dentro de los márgenes seguros.");
            }

            sb.AppendLine("3. **Impulso Comercial:** Promocionar los productos más vendidos en combo con complementos de menor rotación para maximizar el margen bruto.");

            return sb.ToString();
        }

        public async Task<DatosContextoNegocio> RecopilarDatosNegocioAsync(CancellationToken cancellationToken = default)
        {
            await using var ctx = new AppDbContext();
            var datos = new DatosContextoNegocio();

            try
            {

                var productos = await ctx.Productos
                    .AsNoTracking()
                    .Include(p => p.ProductosProveedor)
                    .ThenInclude(pp => pp.Proveedor)
                    .OrderBy(p => p.Categoria)
                    .ThenBy(p => p.Nombre)
                    .ToListAsync(cancellationToken);

                datos.TotalProductos = productos.Count;

                datos.CatalogoProductos = productos.Select(p => new ProductoInfoContexto
                {
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Categoria = p.Categoria ?? "General",
                    StockActual = p.StockActual,
                    StockMinimo = p.StockMinimo,
                    PrecioUnitario = p.PrecioUnitario,
                    UnidadMedida = string.IsNullOrWhiteSpace(p.UnidadMedida) ? "Unidad" : p.UnidadMedida,
                    ProveedorHabitual = p.ProductosProveedor?.FirstOrDefault(pp => pp.EsHabitual)?.Proveedor?.Nombre 
                                        ?? p.ProductosProveedor?.FirstOrDefault()?.Proveedor?.Nombre 
                                        ?? "Distribuidor General"
                }).ToList();

                var criticos = productos
                    .Where(p => p.StockMinimo > 0 && p.StockActual <= p.StockMinimo)
                    .OrderBy(p => p.StockActual)
                    .ToList();

                datos.ProductosStockBajo = criticos.Count;
                datos.ListaProductosStockCritico = criticos.Select(p => 
                    $"{p.Nombre} (Stock: {p.StockActual:0.##} {p.UnidadMedida} / Mín: {p.StockMinimo:0.##})"
                ).ToList();

                var ventasRecientes = await ctx.Ventas
                    .AsNoTracking()
                    .Include(v => v.Detalles)
                    .Include(v => v.ClienteCredito)
                    .OrderByDescending(v => v.Fecha)
                    .Take(30)
                    .ToListAsync(cancellationToken);

                datos.HistorialVentasRecientes = ventasRecientes.Select(v => new VentaInfoContexto
                {
                    NumeroComprobante = !string.IsNullOrWhiteSpace(v.NumeroTicket) ? v.NumeroTicket : $"TKT-{v.Id:D6}",
                    Fecha = v.Fecha,
                    Total = v.Total,
                    TipoVenta = v.TipoVenta ?? "Efectivo",
                    Cliente = v.ClienteCredito?.Nombre ?? (!string.IsNullOrWhiteSpace(v.ClienteNombre) ? v.ClienteNombre : "Público Mostrador"),
                    Items = v.Detalles?.Select(d => $"{d.Cantidad:0.##} {d.UnidadMedida} x {d.ProductoNombre} (P.U: S/ {d.PrecioUnitario:N2})").ToList() ?? new List<string>()
                }).ToList();

                var hace7Dias = DateTime.Today.AddDays(-6);
                var ventasSemana = ventasRecientes.Where(v => v.Fecha >= hace7Dias).ToList();
                if (!ventasSemana.Any())
                {
                    ventasSemana = await ctx.Ventas
                        .AsNoTracking()
                        .Include(v => v.Detalles)
                        .Where(v => v.Fecha >= hace7Dias)
                        .ToListAsync(cancellationToken);
                }

                datos.CantidadTicketsSemana = ventasSemana.Count;
                datos.TotalVentasSemana = Math.Round(ventasSemana.Sum(v => v.Total), 2);
                datos.TicketPromedioSemana = datos.CantidadTicketsSemana > 0 
                    ? Math.Round(datos.TotalVentasSemana / datos.CantidadTicketsSemana, 2) 
                    : 0m;

                datos.VentasEfectivoSemana = Math.Round(ventasSemana.Where(v => v.TipoVenta == TipoVentaConsts.Efectivo).Sum(v => v.Total), 2);
                datos.VentasDigitalSemana = Math.Round(ventasSemana.Where(v => v.TipoVenta == "Digital" || v.TipoVenta == "Yape" || v.TipoVenta == "Plin" || v.TipoVenta == "Transferencia").Sum(v => v.Total), 2);
                datos.VentasCreditoSemana = Math.Round(ventasSemana.Where(v => v.TipoVenta == TipoVentaConsts.Credito).Sum(v => v.Total), 2);

                var topProds = ventasSemana
                    .SelectMany(v => v.Detalles ?? Enumerable.Empty<DetalleVenta>())
                    .GroupBy(d => d.ProductoNombre)
                    .Select(g => new { Nombre = g.Key, Cantidad = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Subtotal) })
                    .OrderByDescending(x => x.Total)
                    .Take(5)
                    .Select(x => $"{x.Nombre} ({x.Cantidad:0.##} und - S/ {x.Total:N2})")
                    .ToList();

                datos.TopProductosVendidos = topProds;

                var proveedores = await ctx.Proveedores.AsNoTracking().ToListAsync(cancellationToken);
                datos.DirectorioProveedores = proveedores.Select(pr => new ProveedorInfoContexto
                {
                    Nombre = pr.Nombre,
                    Contacto = pr.Contacto ?? "",
                    Telefono = pr.Telefono ?? "",
                    WhatsApp = pr.WhatsApp ?? "",
                    Correo = pr.Correo ?? "",
                    Direccion = pr.Direccion ?? "",
                    LineasSuministro = pr.Observaciones ?? ""
                }).ToList();

                var clientesCredito = await ctx.ClientesCredito.AsNoTracking().ToListAsync(cancellationToken);
                var conDeuda = clientesCredito.Where(c => c.DeudaActual > 0).OrderByDescending(c => c.DeudaActual).ToList();
                datos.ClientesConDeuda = conDeuda.Count;
                datos.DeudaTotalClientes = Math.Round(conDeuda.Sum(c => c.DeudaActual), 2);
                datos.ClientesMayorDeuda = conDeuda.Select(c => 
                    $"{c.Nombre} (Debe: S/ {c.DeudaActual:N2} de S/ {c.LimiteCredito:N2})"
                ).ToList();

                var lotes = await ctx.LotesProducto.AsNoTracking().Where(l => l.Estado == "Activo").ToListAsync(cancellationToken);
                datos.LotesActivos = lotes.Count;
                datos.LotesPorAgotarse = lotes.Count(l => l.CantidadDisponible <= (l.CantidadInicial * 0.20m));

                var ultimoArqueo = await ctx.ArqueosCaja.AsNoTracking().OrderByDescending(a => a.FechaHoraRegistro).FirstOrDefaultAsync(cancellationToken);
                if (ultimoArqueo != null)
                {
                    datos.EstadoCajaResumen = $"Arqueo #{ultimoArqueo.Id} ({ultimoArqueo.FechaHoraRegistro:dd/MM/yyyy HH:mm}): Apertura S/ {ultimoArqueo.MontoApertura:N2} | Ventas Efec: S/ {ultimoArqueo.TotalVentasEfectivo:N2} | Abonos Efec: S/ {ultimoArqueo.TotalAbonosEfectivo:N2} | Total Efectivo en Caja: S/ {ultimoArqueo.TotalEfectivoEnCaja:N2}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al recopilar datos de negocio: {ex.Message}");
            }

            return datos;
        }
    }
}
