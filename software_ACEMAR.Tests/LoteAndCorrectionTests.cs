using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using software_ACEMAR.Data;
using software_ACEMAR.Models;
using software_ACEMAR.Services;
using software_ACEMAR.ViewModels;

namespace software_ACEMAR.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class LoteAndCorrectionTests
    {
        [TestMethod]
        public async Task RegistrarLote_IncrementsProductStock_AndEnforcesFifo()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var loteService = new LoteService();
            var inventoryService = new InventoryService();

            var producto = new Producto
            {
                Codigo = $"TEST-LOTE-{Guid.NewGuid():N}",
                Nombre = "Cemento Tipo I Ensilado",
                Categoria = "Construcción",
                PrecioUnitario = 32.50m,
                StockActual = 0,
                StockMinimo = 5,
                CuMax = 200,
                UnidadMedida = "Bolsa"
            };

            var (creado, err) = await inventoryService.CrearProductoAsync(producto);
            Assert.IsTrue(creado, err);

            // Crear Lote 1 (primer lote en ingresar)
            var lote1 = new LoteProducto
            {
                ProductoId = producto.Id,
                CantidadInicial = 50,
                CantidadDisponible = 50,
                CostoCompra = 20.00m,
                PrecioVenta = 32.50m,
                FechaIngreso = DateTime.Now.AddDays(-5),
                Estado = "Activo"
            };
            var (lote1Exito, lote1Err) = await loteService.CrearLoteAsync(lote1);
            Assert.IsTrue(lote1Exito, lote1Err);

            // Crear Lote 2 (segundo lote en ingresar)
            var lote2 = new LoteProducto
            {
                ProductoId = producto.Id,
                CantidadInicial = 30,
                CantidadDisponible = 30,
                CostoCompra = 22.00m,
                PrecioVenta = 34.00m,
                FechaIngreso = DateTime.Now,
                Estado = "Activo"
            };
            var (lote2Exito, lote2Err) = await loteService.CrearLoteAsync(lote2);
            Assert.IsTrue(lote2Exito, lote2Err);

            // Consumir 20 unidades en FIFO
            var (fifoExito, loteUsado, precioVenta, fifoErr) = await loteService.ConsumarStockFifoAsync(producto.Id, 20);
            Assert.IsTrue(fifoExito, fifoErr);
            Assert.IsNotNull(loteUsado);
            Assert.AreEqual(lote1.Id, loteUsado.Id); // Debe ser Lote 1 por FIFO
            Assert.AreEqual(30m, loteUsado.CantidadDisponible); // 50 - 20 = 30
        }

        [TestMethod]
        public void RegistrarProveedor_AndSearch_WorksCorrectly()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var proveedorService = new ProveedorService();
            string nombreUnico = $"PROV-ACEMAR-{Guid.NewGuid():N}";

            var prov = new Proveedor
            {
                Nombre = nombreUnico,
                Contacto = "Juan Pérez",
                Telefono = "987654321",
                Correo = "ventas@proveedor.com",
                Direccion = "Av. Industrial 123",
                Observaciones = "Proveedor preferente de fierro"
            };

            bool creado = proveedorService.Crear(prov, out string error);
            Assert.IsTrue(creado, error);

            var lista = proveedorService.Buscar(nombreUnico);
            Assert.IsTrue(lista.Any());
            Assert.AreEqual(nombreUnico, lista.First().Nombre);
        }

        [TestMethod]
        public async Task CorregirVenta_GeneratesAuditTrail()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var loteService = new LoteService();
            var salesService = new SalesService();
            var correctionService = new VentaCorrectionService(loteService);

            int productoId;
            string productoNombre = "Pintura Látex Blanca 4L";

            using (var ctx = new AppDbContext())
            {
                var prod = new Producto
                {
                    Codigo = $"PROD-AUD-{Guid.NewGuid():N}",
                    Nombre = productoNombre,
                    Categoria = "Pinturas",
                    PrecioUnitario = 45.00m,
                    StockActual = 50,
                    StockMinimo = 5,
                    CuMax = 100,
                    UnidadMedida = "Galón"
                };
                ctx.Productos.Add(prod);
                await ctx.SaveChangesAsync();
                productoId = prod.Id;
            }

            var venta = new Venta
            {
                Fecha = DateTime.Now,
                Total = 90.00m,
                TipoVenta = "Efectivo",
                ClienteNombre = "Cliente Test Auditoría",
                UsuarioVendedor = "Vendedor Test",
                Detalles = new List<DetalleVenta>
                {
                    new DetalleVenta
                    {
                        ProductoId = productoId,
                        ProductoNombre = productoNombre,
                        Cantidad = 2,
                        PrecioUnitario = 45.00m
                    }
                }
            };

            var (ventaOk, ventaErr) = await salesService.ProcesarVentaAsync(venta);
            Assert.IsTrue(ventaOk, ventaErr);

            // Corregir la venta
            var nuevosDetalles = new List<DetalleVenta>
            {
                new DetalleVenta
                {
                    ProductoId = productoId,
                    ProductoNombre = productoNombre,
                    Cantidad = 1, // Reducir a 1 galón
                    PrecioUnitario = 45.00m
                }
            };

            var (corrOk, corrErr) = await correctionService.CorregirVentaAsync(venta.Id, nuevosDetalles, "Cliente Test Auditado", "Admin Auditor");
            Assert.IsTrue(corrOk, corrErr);

            var auditoria = correctionService.ObtenerAuditoria(venta.Id);
            Assert.IsTrue(auditoria.Any());
            Assert.IsTrue(auditoria.First().Descripcion.Contains("Modificación"));
        }

        [TestMethod]
        public async Task ReglaPvpYFifoCostos_CalculaPrecioYGananciasCorrectamente()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var loteService = new LoteService();
            var salesService = new SalesService();
            var inventoryService = new InventoryService();

            var producto = new Producto
            {
                Codigo = $"TEST-PVP-{Guid.NewGuid():N}",
                Nombre = "Arena Fina Cantera",
                Categoria = "Agregados",
                PrecioUnitario = 50.00m,
                StockActual = 0,
                StockMinimo = 5,
                CuMax = 500,
                UnidadMedida = "Metro"
            };
            var (prodOk, prodErr) = await inventoryService.CrearProductoAsync(producto);
            Assert.IsTrue(prodOk, prodErr);

            // Lote 1: 10 Metros @ Costo S/ 30, PVP S/ 50
            var lote1 = new LoteProducto
            {
                ProductoId = producto.Id,
                CantidadInicial = 10,
                CantidadDisponible = 10,
                CostoCompra = 30.00m,
                PrecioVenta = 50.00m,
                FechaIngreso = DateTime.Now.AddDays(-2),
                Estado = "Activo"
            };
            await loteService.CrearLoteAsync(lote1);

            // Lote 2: 10 Metros @ Costo S/ 20 (más barato), PVP S/ 40
            var lote2 = new LoteProducto
            {
                ProductoId = producto.Id,
                CantidadInicial = 10,
                CantidadDisponible = 10,
                CostoCompra = 20.00m,
                PrecioVenta = 40.00m,
                FechaIngreso = DateTime.Now,
                Estado = "Activo"
            };
            await loteService.CrearLoteAsync(lote2);

            // Al ingresar lote más barato, PVP DEBE MANTENERSE en S/ 50.00 (el más caro del stock activo)
            decimal pvpActual = salesService.ObtenerPrecioVigente(producto.Id);
            Assert.AreEqual(50.00m, pvpActual);

            // Venta de 15 Metros: 10 del Lote 1 (costo 30) y 5 del Lote 2 (costo 20), cobrados todos a PVP S/ 50
            var venta = new Venta
            {
                Fecha = DateTime.Now,
                Total = 15 * 50.00m, // 750.00
                TipoVenta = "Efectivo",
                UsuarioVendedor = "Vendedor Test",
                Detalles = new List<DetalleVenta>
                {
                    new DetalleVenta
                    {
                        ProductoId = producto.Id,
                        ProductoNombre = producto.Nombre,
                        Cantidad = 15,
                        PrecioUnitario = 50.00m,
                        UnidadMedida = "Metro"
                    }
                }
            };

            var (ventaOk, ventaErr) = await salesService.ProcesarVentaAsync(venta);
            Assert.IsTrue(ventaOk, ventaErr);

            // Verificar que se dividió en 2 líneas FIFO con su respectivo costo de compra
            Assert.AreEqual(2, venta.Detalles.Count);
            var linea1 = venta.Detalles.First(d => d.LoteProductoId == lote1.Id);
            var linea2 = venta.Detalles.First(d => d.LoteProductoId == lote2.Id);

            Assert.AreEqual(10m, linea1.Cantidad);
            Assert.AreEqual(30.00m, linea1.CostoUnitarioCompra);
            Assert.AreEqual(50.00m, linea1.PrecioUnitario);

            Assert.AreEqual(5m, linea2.Cantidad);
            Assert.AreEqual(20.00m, linea2.CostoUnitarioCompra);
            Assert.AreEqual(50.00m, linea2.PrecioUnitario);

            // Como se agotó el Lote 1 (más caro), ahora el PVP del stock restante debe bajar a S/ 40.00
            decimal pvpDespuesAgotarLote1 = salesService.ObtenerPrecioVigente(producto.Id);
            Assert.AreEqual(40.00m, pvpDespuesAgotarLote1);
        }

        [TestMethod]
        public void CartDuplicateCheck_MultipleUnidadesMedida_CreatesSeparateLines()
        {
            var inventoryService = new InventoryService();
            var salesService = new SalesService();
            var authService = new AuthService();
            var creditService = new CreditService(authService);
            var proformaService = new ProformaService();
            var pdfService = new PdfService();

            var vm = new PosViewModel(inventoryService, salesService, authService, creditService, proformaService, pdfService);

            var prodArena = new Producto
            {
                Id = 999,
                Codigo = "ARENA-01",
                Nombre = "Arena de Construcción",
                PrecioUnitario = 60.00m,
                StockActual = 100,
                UnidadMedida = "Metro",
                FactoresPorUnidad = "{\"Volquetada\":4.0}"
            };

            // Simular agregar 1 Volquetada
            vm.ProductoSeleccionado = prodArena;
            vm.UnidadMedidaSeleccionada = "Volquetada";
            vm.CantidadTexto = "1";
            vm.AgregarAlTicketCommand.Execute(null);

            // Simular agregar 3 Metros
            vm.UnidadMedidaSeleccionada = "Metro";
            vm.CantidadTexto = "3";
            vm.AgregarAlTicketCommand.Execute(null);

            // Debe haber 2 líneas separadas en el ticket
            Assert.AreEqual(2, vm.TicketActual.Count);
            Assert.AreEqual("Volquetada", vm.TicketActual[0].UnidadMedida);
            Assert.AreEqual(1m, vm.TicketActual[0].Cantidad);

            Assert.AreEqual("Metro", vm.TicketActual[1].UnidadMedida);
            Assert.AreEqual(3m, vm.TicketActual[1].Cantidad);
        }
    }
}

