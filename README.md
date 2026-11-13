# Sistema de Gestión Comercial y Punto de Venta - ACEMAR S.A.C.

Software integral de escritorio para control de inventarios por lotes FIFO, punto de venta (POS), facturación electrónica local, cotizaciones / proformas, arqueo de caja y módulo de créditos/fiados, desarrollado bajo plataforma .NET 8 WPF con persistencia en base de datos SQLite.

> **NOTA IMPORTANTE SOBRE EL DESPLIEGUE:**  
> La versión ejecutable operativa y lista para producción se encuentra instalada y configurada en la PC local del cliente.

---

## 🚀 Formas de Ejecución

### Opción 1: Ejecución directa mediante el archivo ejecutable (`.exe`)
1. Abrir el explorador de archivos de Windows y dirigirse a la siguiente ruta dentro del proyecto:
   ```text
   software_ACEMAR\bin\Debug\net8.0-windows\
   ```
2. Ubicar el archivo ejecutable:
   ```text
   software_ACEMAR.exe
   ```
3. Hacer doble clic sobre `software_ACEMAR.exe` para iniciar la aplicación.

---

### Opción 2: Ejecución desde la consola / terminal

1. Abrir una ventana de comandos (**PowerShell** o **CMD**) en la carpeta raíz del proyecto.
2. Ejecutar el siguiente comando para compilar e iniciar el sistema:
   ```bash
   dotnet run --project software_ACEMAR
   ```

---

## 📌 Resumen de Módulos y Capacidades

- **Punto de Venta (POS):** Facturación rápida con soporte para múltiples unidades de medida fraccionarias (Unidad, Millar, Ciento, Metro, Bolsa, Volquetada, Carretilla, Lata, etc.), cálculo de precios dinámicos y emisión inmediata de comprobantes.
- **Control de Inventario y Lotes (FIFO):** Trazabilidad de compras por lotes, costo unitario de adquisición, fechas de ingreso y despacho automatizado bajo el método FIFO (primero en entrar, primero en salir).
- **Emisión de Comprobantes:** Generación de Boletas de Venta en PDF formato A4 oficial, Tickets Térmicos de 80 mm con código QR para impresión rápida y Proformas Comerciales.
- **Auditoría Interna de Ventas:** Supervisión de ventas emitidas con visualización exclusiva del lote y proveedor asignado a cada producto despachado, permitiendo correcciones auditadas con registro de usuario y motivo.
- **Créditos y Fiados:** Gestión de líneas de crédito por cliente, registro de entregas al fiado y control de cobranzas con abonos parciales o totales.
- **Arqueo y Cierre de Caja:** Registro de aperturas, ingresos, salidas operativas y conciliación de saldo en efectivo y digital (Yape / Plin / Transferencias).
- **Directorio de Proveedores:** Registro y vinculación comercial de proveedores del rubro de construcción y ferretería.
- **Asistente Ejecutivo de Consultas:** Motor de análisis y diagnóstico comercial integrado para consultas en lenguaje natural del estado del negocio, inventario y ventas.

---

## 🔑 Credenciales de Acceso Predeterminadas

| Rol | Usuario | Contraseña |
| :--- | :--- | :--- |
| **Administrador / Gerencia** | `admin` | `admin123` |
| **Vendedor / Cajero** | `vendedor` | `vendedor123` |

---

## 🛠️ Requisitos del Entorno

- Sistema Operativo: Windows 10 / Windows 11 (64-bit)
- Entorno de Ejecución: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) para Windows x64.
