using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Models;

namespace software_ACEMAR.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
        public DbSet<ClienteCredito> ClientesCredito => Set<ClienteCredito>();
        public DbSet<AbonoCredito> AbonosCredito => Set<AbonoCredito>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Proforma> Proformas => Set<Proforma>();
        public DbSet<DetalleProforma> DetallesProforma => Set<DetalleProforma>();
        public DbSet<ArqueoCaja> ArqueosCaja => Set<ArqueoCaja>();

        public static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "acemar.db");

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={DbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precisiones decimales para SQLite/EF Core
            modelBuilder.Entity<Producto>()
                .Property(p => p.PrecioUnitario).HasPrecision(18, 2);
            modelBuilder.Entity<Producto>()
                .Property(p => p.StockActual).HasPrecision(18, 2);
            modelBuilder.Entity<Producto>()
                .Property(p => p.StockMinimo).HasPrecision(18, 2);
            modelBuilder.Entity<Producto>()
                .Property(p => p.CuMax).HasPrecision(18, 2);

            modelBuilder.Entity<ClienteCredito>()
                .Property(c => c.LimiteCredito).HasPrecision(18, 2);
            modelBuilder.Entity<ClienteCredito>()
                .Property(c => c.DeudaActual).HasPrecision(18, 2);

            modelBuilder.Entity<AbonoCredito>()
                .Property(a => a.Monto).HasPrecision(18, 2);

            modelBuilder.Entity<Venta>()
                .Property(v => v.Total).HasPrecision(18, 2);

            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.Cantidad).HasPrecision(18, 2);
            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.PrecioUnitario).HasPrecision(18, 2);

            modelBuilder.Entity<Proforma>()
                .Property(p => p.Total).HasPrecision(18, 2);

            modelBuilder.Entity<DetalleProforma>()
                .Property(d => d.Cantidad).HasPrecision(18, 2);
            modelBuilder.Entity<DetalleProforma>()
                .Property(d => d.PrecioUnitario).HasPrecision(18, 2);

            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.MontoApertura).HasPrecision(18, 2);
            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.TotalVentasEfectivo).HasPrecision(18, 2);
            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.TotalVentasCredito).HasPrecision(18, 2);
            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.TotalAbonosEfectivo).HasPrecision(18, 2);
        }

        public void InitializeDatabase()
        {
            Database.EnsureCreated();

            // Seed inicial de productos ferreteros ampliados (45+ productos de prueba)
            if (!Productos.Any())
            {
                Productos.AddRange(
                    // ── CONSTRUCCIÓN ──────────────────────────────────────────────────────────
                    new Producto { Codigo = "CEM-001", Nombre = "Cemento Sol Tipo I (50kg)", Categoria = "Construcción", PrecioUnitario = 32.50m, StockActual = 35.0m, StockMinimo = 15.0m, CuMax = 100.0m, UnidadMedida = "Bolsa" },
                    new Producto { Codigo = "CEM-002", Nombre = "Cemento Andino Tipo V (50kg)", Categoria = "Construcción", PrecioUnitario = 36.00m, StockActual = 20.0m, StockMinimo = 10.0m, CuMax = 80.0m, UnidadMedida = "Bolsa" },
                    new Producto { Codigo = "ARE-003", Nombre = "Arena Gruesa Lavada", Categoria = "Construcción", PrecioUnitario = 6.50m, StockActual = 50.0m, StockMinimo = 20.0m, CuMax = 150.0m, UnidadMedida = "Bolsa" },
                    new Producto { Codigo = "LAD-004", Nombre = "Ladrillo King Kong 18 Huecos", Categoria = "Construcción", PrecioUnitario = 1.20m, StockActual = 1200.0m, StockMinimo = 500.0m, CuMax = 3000.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "YES-023", Nombre = "Yeso Blanco Industrial (Bolsa 20kg)", Categoria = "Construcción", PrecioUnitario = 12.50m, StockActual = 28.0m, StockMinimo = 10.0m, CuMax = 80.0m, UnidadMedida = "Bolsa" },
                    new Producto { Codigo = "MAL-024", Nombre = "Malla Electrosoldada 15x15 (m²)", Categoria = "Construcción", PrecioUnitario = 18.00m, StockActual = 40.0m, StockMinimo = 15.0m, CuMax = 120.0m, UnidadMedida = "Metros" },
                    new Producto { Codigo = "FIE-025", Nombre = "Fierro Corrugado 3/8\" x 9m (Varilla)", Categoria = "Construcción", PrecioUnitario = 21.00m, StockActual = 65.0m, StockMinimo = 20.0m, CuMax = 200.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "FIE-026", Nombre = "Fierro Corrugado 1/2\" x 9m (Varilla)", Categoria = "Construcción", PrecioUnitario = 37.50m, StockActual = 48.0m, StockMinimo = 15.0m, CuMax = 150.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "BLO-027", Nombre = "Bloqueta de Concreto 15cm", Categoria = "Construcción", PrecioUnitario = 2.50m, StockActual = 500.0m, StockMinimo = 200.0m, CuMax = 2000.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "MOR-028", Nombre = "Mortero Seco Klaukol 25kg", Categoria = "Construcción", PrecioUnitario = 24.00m, StockActual = 18.0m, StockMinimo = 8.0m, CuMax = 60.0m, UnidadMedida = "Bolsa" },

                    // ── FIJACIONES ────────────────────────────────────────────────────────────
                    new Producto { Codigo = "CLA-005", Nombre = "Clavos de Acero 2\" (Kg)", Categoria = "Fijaciones", PrecioUnitario = 7.20m, StockActual = 8.5m, StockMinimo = 10.0m, CuMax = 50.0m, UnidadMedida = "Kg" },
                    new Producto { Codigo = "CLA-006", Nombre = "Clavos para Madera 3\" (Kg)", Categoria = "Fijaciones", PrecioUnitario = 6.80m, StockActual = 42.0m, StockMinimo = 15.0m, CuMax = 80.0m, UnidadMedida = "Kg" },
                    new Producto { Codigo = "TOR-007", Nombre = "Tornillos Autoperforantes 2\" (Ciento)", Categoria = "Fijaciones", PrecioUnitario = 14.50m, StockActual = 18.0m, StockMinimo = 10.0m, CuMax = 60.0m, UnidadMedida = "Ciento" },
                    new Producto { Codigo = "TOR-029", Nombre = "Tornillos Hex 5/16\" x 2\" c/ Tuerca (10u)", Categoria = "Fijaciones", PrecioUnitario = 4.80m, StockActual = 35.0m, StockMinimo = 15.0m, CuMax = 100.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "TAC-030", Nombre = "Taco Fischer SX10 (Bolsa 50u)", Categoria = "Fijaciones", PrecioUnitario = 9.50m, StockActual = 22.0m, StockMinimo = 8.0m, CuMax = 80.0m, UnidadMedida = "Bolsa" },
                    new Producto { Codigo = "AND-031", Nombre = "Anclaje Químico Hilti RE 500 (Cartucho)", Categoria = "Fijaciones", PrecioUnitario = 85.00m, StockActual = 5.0m, StockMinimo = 3.0m, CuMax = 20.0m, UnidadMedida = "Unidad" },

                    // ── GASFITERÍA ────────────────────────────────────────────────────────────
                    new Producto { Codigo = "TUB-008", Nombre = "Tubo PVC 1/2\" Clase 10 Agua (Unidad)", Categoria = "Gasfitería", PrecioUnitario = 14.80m, StockActual = 4.0m, StockMinimo = 12.0m, CuMax = 60.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "TUB-009", Nombre = "Tubo PVC 2\" Desagüe Liviano (Unidad)", Categoria = "Gasfitería", PrecioUnitario = 22.50m, StockActual = 15.0m, StockMinimo = 8.0m, CuMax = 50.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "PEG-010", Nombre = "Pegamento PVC Oatey 1/4 Galón", Categoria = "Gasfitería", PrecioUnitario = 28.00m, StockActual = 9.0m, StockMinimo = 5.0m, CuMax = 30.0m, UnidadMedida = "Galón" },
                    new Producto { Codigo = "COD-032", Nombre = "Codo PVC 90° 1/2\" (Gasfitería)", Categoria = "Gasfitería", PrecioUnitario = 1.20m, StockActual = 80.0m, StockMinimo = 30.0m, CuMax = 200.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "TEE-033", Nombre = "Tee PVC 1/2\" (Gasfitería)", Categoria = "Gasfitería", PrecioUnitario = 1.50m, StockActual = 60.0m, StockMinimo = 25.0m, CuMax = 200.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "LLA-034", Nombre = "Llave de Paso 1/2\" Bronce FV", Categoria = "Gasfitería", PrecioUnitario = 18.50m, StockActual = 14.0m, StockMinimo = 6.0m, CuMax = 50.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "SIF-035", Nombre = "Sifón Botella PVC 1 1/2\" para Lavatorio", Categoria = "Gasfitería", PrecioUnitario = 12.00m, StockActual = 10.0m, StockMinimo = 4.0m, CuMax = 40.0m, UnidadMedida = "Unidad" },

                    // ── ELECTRICIDAD ──────────────────────────────────────────────────────────
                    new Producto { Codigo = "CAB-011", Nombre = "Cable Eléctrico Mellizo 2x14 (Metros)", Categoria = "Electricidad", PrecioUnitario = 3.50m, StockActual = 120.0m, StockMinimo = 30.0m, CuMax = 200.0m, UnidadMedida = "Metros" },
                    new Producto { Codigo = "CAB-012", Nombre = "Cable Indeco THW 14 AWG (Metros)", Categoria = "Electricidad", PrecioUnitario = 2.80m, StockActual = 95.0m, StockMinimo = 25.0m, CuMax = 300.0m, UnidadMedida = "Metros" },
                    new Producto { Codigo = "INT-013", Nombre = "Interruptor Simple Bticino Matix", Categoria = "Electricidad", PrecioUnitario = 8.50m, StockActual = 30.0m, StockMinimo = 10.0m, CuMax = 100.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "TOM-014", Nombre = "Tomacorriente Doble C/ Tierra Bticino", Categoria = "Electricidad", PrecioUnitario = 12.00m, StockActual = 25.0m, StockMinimo = 10.0m, CuMax = 80.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "LLA-036", Nombre = "Llaves Térmicas 2x16A (Caja)", Categoria = "Electricidad", PrecioUnitario = 45.00m, StockActual = 8.0m, StockMinimo = 3.0m, CuMax = 30.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "CON-037", Nombre = "Conduit PVC 3/4\" x 3m (Unidad)", Categoria = "Electricidad", PrecioUnitario = 8.00m, StockActual = 40.0m, StockMinimo = 15.0m, CuMax = 100.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "CAJ-038", Nombre = "Caja Octogonal PVC c/ Tapa Ciega", Categoria = "Electricidad", PrecioUnitario = 2.00m, StockActual = 55.0m, StockMinimo = 20.0m, CuMax = 150.0m, UnidadMedida = "Unidad" },

                    // ── HERRAMIENTAS ──────────────────────────────────────────────────────────
                    new Producto { Codigo = "DIS-015", Nombre = "Disco de Corte Metal 4 1/2\" Norton", Categoria = "Herramientas", PrecioUnitario = 6.50m, StockActual = 25.0m, StockMinimo = 8.0m, CuMax = 80.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "DIS-016", Nombre = "Disco Diamantado Continuo 4 1/2\" Bosch", Categoria = "Herramientas", PrecioUnitario = 19.50m, StockActual = 7.0m, StockMinimo = 5.0m, CuMax = 40.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "LLA-020", Nombre = "Llave Francesa Ajustable 10\" Stanley", Categoria = "Herramientas", PrecioUnitario = 38.00m, StockActual = 6.0m, StockMinimo = 3.0m, CuMax = 25.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "FLE-021", Nombre = "Flexómetro 5m Truper Grip", Categoria = "Herramientas", PrecioUnitario = 16.50m, StockActual = 11.0m, StockMinimo = 5.0m, CuMax = 40.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "CAR-022", Nombre = "Carretilla Buggy Reforzada 6 P3", Categoria = "Herramientas", PrecioUnitario = 185.00m, StockActual = 3.0m, StockMinimo = 2.0m, CuMax = 15.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "MAR-039", Nombre = "Martillo Carpintero 16oz Mango Fibra", Categoria = "Herramientas", PrecioUnitario = 28.00m, StockActual = 9.0m, StockMinimo = 4.0m, CuMax = 30.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "PLA-040", Nombre = "Plana de Albañil 8\" Acero Inox", Categoria = "Herramientas", PrecioUnitario = 15.00m, StockActual = 16.0m, StockMinimo = 6.0m, CuMax = 50.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "ESP-041", Nombre = "Espátula Mango Plástico 4\"", Categoria = "Herramientas", PrecioUnitario = 8.50m, StockActual = 20.0m, StockMinimo = 8.0m, CuMax = 60.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "NI-042", Nombre = "Nivel de Burbuja Aluminio 60cm", Categoria = "Herramientas", PrecioUnitario = 32.00m, StockActual = 7.0m, StockMinimo = 3.0m, CuMax = 20.0m, UnidadMedida = "Unidad" },

                    // ── PINTURAS ──────────────────────────────────────────────────────────────
                    new Producto { Codigo = "PIN-017", Nombre = "Pintura Látex Vencedor Blanco (Galón)", Categoria = "Pinturas", PrecioUnitario = 42.00m, StockActual = 14.0m, StockMinimo = 6.0m, CuMax = 50.0m, UnidadMedida = "Galón" },
                    new Producto { Codigo = "BRO-018", Nombre = "Brocha 3\" Cerda Mona Atlas", Categoria = "Pinturas", PrecioUnitario = 8.00m, StockActual = 22.0m, StockMinimo = 8.0m, CuMax = 60.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "ROD-043", Nombre = "Rodillo Lana 9\" para Látex", Categoria = "Pinturas", PrecioUnitario = 14.00m, StockActual = 15.0m, StockMinimo = 6.0m, CuMax = 50.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "ESM-044", Nombre = "Esmalte Alquídico Satinado Negro (Litro)", Categoria = "Pinturas", PrecioUnitario = 19.50m, StockActual = 10.0m, StockMinimo = 4.0m, CuMax = 40.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "LIJ-045", Nombre = "Lija al Agua Grano 120 (Pliego)", Categoria = "Pinturas", PrecioUnitario = 1.80m, StockActual = 60.0m, StockMinimo = 20.0m, CuMax = 200.0m, UnidadMedida = "Unidad" },

                    // ── CERRAJERÍA Y SEGURIDAD ────────────────────────────────────────────────
                    new Producto { Codigo = "CAN-019", Nombre = "Candado de Latón Forte 50mm", Categoria = "Cerrajería", PrecioUnitario = 26.00m, StockActual = 12.0m, StockMinimo = 5.0m, CuMax = 40.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "BIS-046", Nombre = "Bisagra Alum. Capuchina 3\" (Par)", Categoria = "Cerrajería", PrecioUnitario = 5.50m, StockActual = 30.0m, StockMinimo = 12.0m, CuMax = 100.0m, UnidadMedida = "Par" },
                    new Producto { Codigo = "PAS-047", Nombre = "Pasador Aldaba de Acero 4\" (Unidad)", Categoria = "Cerrajería", PrecioUnitario = 7.00m, StockActual = 25.0m, StockMinimo = 10.0m, CuMax = 80.0m, UnidadMedida = "Unidad" },
                    new Producto { Codigo = "PER-048", Nombre = "Perfil Ángulo de Aluminio 1\" x 6m", Categoria = "Cerrajería", PrecioUnitario = 28.00m, StockActual = 8.0m, StockMinimo = 4.0m, CuMax = 30.0m, UnidadMedida = "Unidad" }
                );
            }

            // Seed inicial de clientes con crédito (7 clientes de muestra)
            if (!ClientesCredito.Any())
            {
                ClientesCredito.AddRange(
                    new ClienteCredito
                    {
                        Nombre = "Juan Pérez (Maestro de Obra)",
                        Documento = "45892147",
                        Telefono = "984-512-365",
                        LimiteCredito = 1500.00m,
                        DeudaActual = 450.00m,
                        UltimaActualizacion = DateTime.Now.AddDays(-2)
                    },
                    new ClienteCredito
                    {
                        Nombre = "Constructora Los Andes S.A.C.",
                        Documento = "20601248591",
                        Telefono = "951-748-236",
                        LimiteCredito = 5000.00m,
                        DeudaActual = 1280.00m,
                        UltimaActualizacion = DateTime.Now.AddDays(-5)
                    },
                    new ClienteCredito
                    {
                        Nombre = "Taller Metalmecánica Rodríguez",
                        Documento = "10457812963",
                        Telefono = "974-125-896",
                        LimiteCredito = 800.00m,
                        DeudaActual = 210.50m,
                        UltimaActualizacion = DateTime.Now.AddDays(-1)
                    },
                    new ClienteCredito
                    {
                        Nombre = "María Quispe Flores",
                        Documento = "47123589",
                        Telefono = "955-321-784",
                        LimiteCredito = 600.00m,
                        DeudaActual = 185.00m,
                        UltimaActualizacion = DateTime.Now.AddDays(-3)
                    },
                    new ClienteCredito
                    {
                        Nombre = "Empresa Ingeniería Civil Norte EIRL",
                        Documento = "20578412365",
                        Telefono = "948-741-236",
                        LimiteCredito = 8000.00m,
                        DeudaActual = 3650.00m,
                        UltimaActualizacion = DateTime.Now.AddDays(-7)
                    },
                    new ClienteCredito
                    {
                        Nombre = "Pedro Mamani Condori",
                        Documento = "41587236",
                        Telefono = "962-485-127",
                        LimiteCredito = 500.00m,
                        DeudaActual = 0.00m,
                        UltimaActualizacion = DateTime.Now.AddDays(-10)
                    },
                    new ClienteCredito
                    {
                        Nombre = "Servicios Generales Torres & Hijos",
                        Documento = "20412587963",
                        Telefono = "914-256-387",
                        LimiteCredito = 2500.00m,
                        DeudaActual = 920.75m,
                        UltimaActualizacion = DateTime.Now.AddDays(-4)
                    }
                );
            }

            // Seed inicial de usuarios con credencial para Gerente: passowrd "acemar"
            if (!Usuarios.Any())
            {
                Usuarios.AddRange(
                    new Usuario
                    {
                        NombreUsuario = "Gerente",
                        NombreCompleto = "Gerente General ACEMAR",
                        Password = "acemar",
                        Rol = "Administrador"
                    },
                    new Usuario
                    {
                        NombreUsuario = "vendedor",
                        NombreCompleto = "Carlos Vendedor",
                        Password = "123",
                        Rol = "Vendedor"
                    }
                );
            }
            else
            {
                // Asegurar que el usuario Gerente exista con su password
                var gerente = Usuarios.FirstOrDefault(u => u.NombreUsuario.ToLower() == "gerente" || u.Rol == "Administrador");
                if (gerente != null)
                {
                    gerente.NombreUsuario = "Gerente";
                    gerente.Password = "acemar";
                    gerente.Rol = "Administrador";
                }
            }

            SaveChanges();
        }
    }
}
