using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using software_ACEMAR.Data;
using software_ACEMAR.Models;
using software_ACEMAR.Services;

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
    }
}
