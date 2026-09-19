using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using software_ACEMAR.Data;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

namespace software_ACEMAR.Tests
{
    [TestClass]
    public class BusinessRulesTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Asegurar que la base de datos de prueba esté inicializada
            using var context = new AppDbContext();
            context.InitializeDatabase();
        }

        [TestMethod]
        public void Regla_POS_01_BusquedaManual_PorNombreOCodigo()
        {
            // Regla POS-01 (Búsqueda): La búsqueda de productos en el POS es manual por nombre
            var inventoryService = new InventoryService();

            var resultadosPorNombre = inventoryService.BuscarPorNombre("Cemento");
            Assert.IsTrue(resultadosPorNombre.Any(p => p.Nombre.Contains("Cemento", StringComparison.OrdinalIgnoreCase)));

            var resultadosPorCodigo = inventoryService.BuscarPorNombre("CLA-002");
            Assert.IsTrue(resultadosPorCodigo.Any(p => p.Codigo == "CLA-002"));
        }

        [TestMethod]
        public void Regla_POS_02_CalculoFraccionesDecimales()
        {
            // Regla POS-02 (Fracciones): Permitir ingreso de cantidades en decimales (double o decimal),
            // multiplicando matemáticamente por el precio unitario.
            var salesService = new SalesService();

            decimal cantidad1 = 2.5m; // 2.5 Kg
            decimal precio1 = 7.20m;
            decimal subtotal1 = salesService.MultiplicarPrecioFraccion(cantidad1, precio1);
            Assert.AreEqual(18.00m, subtotal1);

            decimal cantidad2 = 0.75m; // 0.75 metros o fracción
            decimal precio2 = 14.80m;
            decimal subtotal2 = salesService.MultiplicarPrecioFraccion(cantidad2, precio2);
            Assert.AreEqual(11.10m, subtotal2);
        }

        [TestMethod]
        public void Regla_INV_01_BloqueoVenta_StockInsuficiente()
        {
            // Regla INV-01 (Bloqueo de Venta): Si cantidad_solicitada > stock_actual,
            // bloquear la acción y lanzar mensaje exacto: "Stock insuficiente".
            using var context = new AppDbContext();
            var producto = context.Productos.First(p => p.Codigo == "TUB-003"); // StockActual = 4.0
            var salesService = new SalesService();

            // Solicitar 5.0 (mayor al stock actual de 4.0)
            bool esValido = salesService.ValidarStockDisponible(producto.Id, 5.0m, out string mensajeError);

            Assert.IsFalse(esValido);
            Assert.AreEqual("Stock insuficiente", mensajeError);
        }

        [TestMethod]
        public void Regla_INV_02_CuMax_LimiteAlmacenamientoSuperado()
        {
            // Regla INV-02 (CuMax): Al ingresar nueva mercadería al almacén,
            // si stock_actual + ingreso > CuMax, lanzar: "Límite físico de almacenamiento superado".
            using var context = new AppDbContext();
            var producto = context.Productos.First(p => p.Codigo == "CEM-001"); // StockActual = 35.0, CuMax = 100.0
            var inventoryService = new InventoryService();

            // Intentar ingresar 70.0 (35.0 + 70.0 = 105.0 > 100.0)
            bool ingresoPermitido = inventoryService.ValidarIngresoMercaderia(producto.Id, 70.0m, out string mensajeError);

            Assert.IsFalse(ingresoPermitido);
            Assert.AreEqual("Límite físico de almacenamiento superado", mensajeError);
        }

        [TestMethod]
        public void Regla_CRE_01_SeguridadFiados_BloqueoVendedor()
        {
            // Regla CRE-01 (Seguridad Fiados): Si un usuario con rol "Vendedor" intenta abrir la vista de créditos
            // o procesar uno, bloquear acceso con el mensaje: "Permisos insuficientes. Contacte al Administrador".
            var authService = new AuthService();
            authService.CambiarRol("Vendedor");

            bool accesoPermitido = authService.PuedeAccederCreditos(out string mensajeError);

            Assert.IsFalse(accesoPermitido);
            Assert.AreEqual("Permisos insuficientes. Contacte al Administrador", mensajeError);

            // Verificar que como Administrador sí tiene acceso
            authService.CambiarRol("Administrador");
            bool accesoAdmin = authService.PuedeAccederCreditos(out string mensajeAdmin);
            Assert.IsTrue(accesoAdmin);
            Assert.AreEqual(string.Empty, mensajeAdmin);
        }

        [TestMethod]
        public void Regla_Credito_AbonoRestaMatematicaDeuda()
        {
            // Paso 5: Módulo de Créditos (Fiados)
            // Registrar un 'Abono', el cual debe restar matemáticamente la deuda del cliente en la base de datos local
            var authService = new AuthService(); // Admin por defecto
            var creditService = new CreditService(authService);

            using var context = new AppDbContext();
            var cliente = context.ClientesCredito.First(c => c.Documento == "45892147");
            decimal deudaInicial = cliente.DeudaActual;
            decimal montoAbono = 100.00m;

            bool exito = creditService.RegistrarAbono(cliente.Id, montoAbono, "Abono de prueba", "admin", out string error);
            Assert.IsTrue(exito, error);

            // Verificar la resta matemática en la base de datos SQLite
            using var context2 = new AppDbContext();
            var clienteActualizado = context2.ClientesCredito.First(c => c.Documento == "45892147");
            Assert.AreEqual(deudaInicial - montoAbono, clienteActualizado.DeudaActual);
        }
    }
}
