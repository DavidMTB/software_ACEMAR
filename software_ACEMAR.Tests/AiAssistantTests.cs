using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using software_ACEMAR.Data;
using software_ACEMAR.Services;
using software_ACEMAR.ViewModels;

namespace software_ACEMAR.Tests
{
    [TestClass]
    public class AiAssistantTests
    {
        private string _originalKey = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            using var ctx = new AppDbContext();
            ctx.InitializeDatabase();
            var service = new AiAssistantService();
            _originalKey = service.ObtenerApiKey();
        }

        [TestCleanup]
        public void Cleanup()
        {

            if (!string.IsNullOrWhiteSpace(_originalKey))
            {
                var service = new AiAssistantService();
                service.GuardarApiKey(_originalKey);
            }
        }

        private string ObtenerClaveParaPruebas()
        {
            string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(envKey)) return envKey;

            string rutaLocal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_settings.json");
            if (File.Exists(rutaLocal))
            {
                try
                {
                    string json = File.ReadAllText(rutaLocal);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ApiKey", out var k))
                    {
                        return k.GetString() ?? string.Empty;
                    }
                }
                catch { }
            }
            return _originalKey;
        }

        [TestMethod]
        public void AiAssistantService_GuardaYRecuperaApiKey()
        {
            var service = new AiAssistantService();
            string testKey = "AIzaSyTest_1234567890abcdef";

            service.GuardarApiKey(testKey);

            Assert.IsTrue(service.TieneApiKeyConfigurada());
            Assert.AreEqual(testKey, service.ObtenerApiKey());

            service.GuardarApiKey(string.Empty);
            Assert.IsFalse(service.TieneApiKeyConfigurada());
        }

        [TestMethod]
        public async Task AiAssistantService_ModoLocalHeuristico_GeneraInformeValido()
        {
            var service = new AiAssistantService();

            service.GuardarApiKey(string.Empty);

            string diagnostico = await service.GenerarDiagnosticoEjecutivoAsync("General");

            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostico));
            Assert.IsTrue(diagnostico.Contains("REPORTE EJECUTIVO") || diagnostico.Contains("ACEMAR"));
            Assert.IsTrue(diagnostico.Contains("MODO LOCAL") || diagnostico.Contains("DEMO"));
        }

        [TestMethod]
        public async Task AiAssistantService_RecopilarDatosNegocio_ExtraeMetricasReales()
        {
            var service = new AiAssistantService();
            var datos = await service.RecopilarDatosNegocioAsync();

            Assert.IsNotNull(datos);
            Assert.IsTrue(datos.TotalProductos > 0, "Debería haber productos en la base de datos.");
            Assert.IsNotNull(datos.ListaProductosStockCritico);
        }

        [TestMethod]
        public void AiAssistantViewModel_Inicializacion_ContieneMensajeBienvenida()
        {
            var service = new AiAssistantService();
            var vm = new AiAssistantViewModel(service);

            Assert.AreEqual(1, vm.Mensajes.Count);
            Assert.IsTrue(vm.Mensajes[0].EsIa);
            Assert.IsTrue(vm.Mensajes[0].Contenido.Contains("ACEMAR"));
        }

        [TestMethod]
        public async Task AiAssistantViewModel_GenerarDiagnostico_AgregaMensajes()
        {
            var service = new AiAssistantService();
            service.GuardarApiKey(string.Empty); 
            var vm = new AiAssistantViewModel(service);

            vm.GenerarDiagnosticoGeneralCommand.Execute(null);

            int intentos = 0;
            while (vm.EstaCargando && intentos < 30)
            {
                await Task.Delay(100);
                intentos++;
            }

            Assert.IsTrue(vm.Mensajes.Count >= 2, "Debe registrarse el prompt y la respuesta del Copilot.");
            Assert.IsFalse(vm.EstaCargando);
        }

        [TestMethod]
        public void AiAssistantViewModel_LimpiarChat_ReiniciaMensajes()
        {
            var vm = new AiAssistantViewModel(new AiAssistantService());
            vm.Mensajes.Add(new ChatMessageItem { Emisor = "Test", Contenido = "Prueba", EsIa = false });

            vm.LimpiarChatCommand.Execute(null);

            Assert.AreEqual(1, vm.Mensajes.Count);
            Assert.IsTrue(vm.Mensajes[0].EsIa);
        }

        [TestMethod]
        public async Task AiAssistantService_ConsultaProductoLocal_RespondeDetalleArena()
        {
            var service = new AiAssistantService();
            service.GuardarApiKey(string.Empty); 

            string resp = await service.ConsultarAsistenteAsync("cuanto queda de arena");

            Assert.IsFalse(string.IsNullOrWhiteSpace(resp));
            Assert.IsTrue(resp.Contains("Arena") || resp.Contains("ARE-003"), "Debe devolver información de la arena.");
        }

        [TestMethod]
        public async Task AiAssistantService_ConsultaSaludoLocal_RespondeAmablemente()
        {
            var service = new AiAssistantService();
            service.GuardarApiKey(string.Empty);

            string resp = await service.ConsultarAsistenteAsync("hola");

            Assert.IsFalse(string.IsNullOrWhiteSpace(resp));
            Assert.IsTrue(resp.Contains("¡Hola!") && resp.Contains("ACEMAR"));
        }

        [TestMethod]
        public async Task AiAssistantService_ConsultaTiposDeArena_DevuelveArenasRegistradas()
        {
            var service = new AiAssistantService();
            service.GuardarApiKey(string.Empty); 

            string resp = await service.ConsultarAsistenteAsync("dime todos los tipos de arena que vendemos");

            Assert.IsFalse(string.IsNullOrWhiteSpace(resp));
            Assert.IsTrue(resp.Contains("Arena"), "Debe listar productos de arena.");
        }

        [TestMethod]
        public async Task AiAssistantService_ConsultaUltimaVenta_DevuelveInformacionVenta()
        {

            using (var ctx = new AppDbContext())
            {
                if (!ctx.Ventas.Any())
                {
                    ctx.Ventas.Add(new Models.Venta
                    {
                        NumeroTicket = "TKT-000001",
                        Fecha = DateTime.Now,
                        Total = 150.00m,
                        TipoVenta = "Efectivo",
                        ClienteNombre = "Juan Prueba",
                        UsuarioVendedor = "Gerente"
                    });
                    ctx.SaveChanges();
                }
            }

            var service = new AiAssistantService();
            service.GuardarApiKey(string.Empty);

            string respUltima = await service.ConsultarAsistenteAsync("cual fue la ultima venta");
            Assert.IsFalse(string.IsNullOrWhiteSpace(respUltima));
            Assert.IsTrue(respUltima.Contains("Comprobante") || respUltima.Contains("VENTA") || respUltima.Contains("TKT"), "Debe responder con información de la última venta.");

            string respLista = await service.ConsultarAsistenteAsync("lista de las ultimas ventas");
            Assert.IsFalse(string.IsNullOrWhiteSpace(respLista));
            Assert.IsTrue(respLista.Contains("VENTAS") || respLista.Contains("TKT"), "Debe listar las ventas recientes.");
        }

        [TestMethod]
        public void AiAssistantService_ProveedoresRegistrados_ExistenYEstanVinculados()
        {
            using var ctx = new AppDbContext();
            var provs = ctx.Proveedores.ToList();
            Assert.IsTrue(provs.Count >= 10, "Deben existir al menos 10 proveedores simulados realistas.");

            Assert.IsTrue(provs.Any(p => p.Nombre.Contains("UNACEM")), "Debe existir proveedor UNACEM.");
            Assert.IsTrue(provs.Any(p => p.Nombre.Contains("Aceros Arequipa")), "Debe existir proveedor Aceros Arequipa.");
            Assert.IsTrue(provs.Any(p => p.Nombre.Contains("Florida") || p.Nombre.Contains("Cantera")), "Debe existir proveedor de cantera.");
            Assert.IsTrue(provs.Any(p => p.Nombre.Contains("Pavco")), "Debe existir proveedor Pavco.");
            Assert.IsTrue(provs.Any(p => p.Nombre.Contains("Indeco")), "Debe existir proveedor Indeco.");

            var relaciones = ctx.ProductosProveedores.ToList();
            Assert.IsTrue(relaciones.Any(), "Los productos deben estar vinculados con sus proveedores habituales.");
        }

        [TestMethod]
        public async Task AiAssistantService_LlamadaRealGemini_PruebaConexion()
        {
            string apiKey = ObtenerClaveParaPruebas();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Assert.Inconclusive("No se proporcionó API key para la prueba en vivo de Gemini (omitida si no hay clave configurada).");
                return;
            }

            var service = new AiAssistantService();
            service.GuardarApiKey(apiKey);

            string resp = await service.ConsultarAsistenteAsync("Dime todos los tipos de arena que vendemos y su stock");
            Console.WriteLine("RESPUESTA GEMINI EN VIVO: " + resp);

            Assert.IsFalse(resp.StartsWith("⚠️ MODO DE RESPALDO"), $"Falló la API de Gemini: {resp}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resp));
            Assert.IsTrue(resp.Contains("Arena") || resp.Contains("ARE-003") || resp.Contains("ARE-004"), "Gemini debe responder con los datos reales de arena de la BD.");
        }
    }
}

