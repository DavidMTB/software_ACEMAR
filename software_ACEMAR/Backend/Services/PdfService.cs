using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using software_ACEMAR.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace software_ACEMAR.Services
{
    public interface IPdfService
    {
        byte[] GenerarPdfBoletaVenta(Venta venta);
        byte[] GenerarPdfTicketTermico(Venta venta);
        byte[] GenerarPdfProforma(Proforma proforma);
        byte[] GenerarPdfReporteStock(List<Producto> productos, List<LoteProducto> lotes);

        string GuardarPdf(byte[] pdfBytes, string subfolder, string nombreArchivo);
        string GuardarYAbrirPdf(byte[] pdfBytes, string subfolder, string nombreArchivo);
        string GenerarHtmlProforma(Proforma proforma);
        string GenerarHtmlVenta(Venta venta);
        string GenerarHtmlReporteStock(List<Producto> productos, List<LoteProducto> lotes);
        Task<string> GuardarDocumentoHtmlAsync(string htmlContent, string subfolder, string fileName);
    }

    public class PdfService : IPdfService
    {
        private readonly string _documentFolder;

        // Datos oficiales institucionales del emisor para SUNAT
        private const string EMISOR_RAZON_SOCIAL = "FERRETERÍA & COMERCIAL ACEMAR S.A.C.";
        private const string EMISOR_NOMBRE_COMERCIAL = "FERRETERÍA ACEMAR";
        private const string EMISOR_RUC = "20601234567";
        private const string EMISOR_DIRECCION = "Av. Principal N° 1254 - Central, Lima, Perú";
        private const string EMISOR_ESTABLECIMIENTO = "Almacén Central & Mostrador Principal";
        private const string EMISOR_CONTACTO = "Teléfono: 94301837 | Correo: ventas@acemar.pe";

        public PdfService()
        {
            // Configurar licencia comunitaria de QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

            string rutaDocumentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _documentFolder = Path.Combine(rutaDocumentos, "ACEMAR_Comprobantes");
            
            if (!Directory.Exists(_documentFolder))
            {
                Directory.CreateDirectory(_documentFolder);
            }
        }

        // ── 1. GENERACIÓN DE BOLETA DE VENTA ELECTRÓNICA (FORMATO A4 SUNAT) ───────
        public byte[] GenerarPdfBoletaVenta(Venta venta)
        {
            var (serie, correlativo, comprobanteFormateado) = ObtenerSerieYCorrelativo(venta);
            string fechaStr = venta.Fecha.ToString("dd/MM/yyyy");
            string horaStr = venta.Fecha.ToString("HH:mm:ss");

            decimal total = venta.Total;
            decimal opGravada = Math.Round(total / 1.18m, 2);
            decimal igv = Math.Round(total - opGravada, 2);

            string clienteNombre = string.IsNullOrWhiteSpace(venta.ClienteNombre) ? "Cliente Mostrador" : venta.ClienteNombre.Trim();
            string clienteDoc = venta.ClienteCredito?.Documento ?? "Sin Documento";
            string tipoDocClienteStr = (clienteDoc != "Sin Documento" && clienteDoc.Length == 11) ? "RUC" : (clienteDoc != "Sin Documento" ? "DNI" : "-");

            // Generar cadena de datos estructurada SUNAT para el QR
            string qrSunatData = $"{EMISOR_RUC}|03|{serie}|{correlativo}|{igv:F2}|{total:F2}|{venta.Fecha:yyyy-MM-dd}|{(tipoDocClienteStr == "RUC" ? "6" : (tipoDocClienteStr == "DNI" ? "1" : "-"))}|{clienteDoc}|";
            byte[] qrBytes = QrCodeHelper.GenerarQrCodeBytes(qrSunatData);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Segoe UI"));

                    // Encabezado Institucional Emisor + RUC Comprobante
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(EMISOR_RAZON_SOCIAL).Bold().FontSize(16).FontColor("#1E3A8A");
                            col.Item().Text($"Nombre Comercial: {EMISOR_NOMBRE_COMERCIAL}").Bold().FontSize(10).FontColor("#334155");
                            col.Item().Text($"Domicilio Fiscal: {EMISOR_DIRECCION}").FontSize(8.5f).FontColor("#64748B");
                            col.Item().Text($"Establecimiento: {EMISOR_ESTABLECIMIENTO}").FontSize(8.5f).FontColor("#64748B");
                            col.Item().Text(EMISOR_CONTACTO).FontSize(8.5f).FontColor("#64748B");
                        });

                        row.ConstantItem(210).Border(1.5f).BorderColor("#1E3A8A").Background("#F8FAFC").Padding(8).Column(col =>
                        {
                            col.Item().AlignCenter().Text($"R.U.C. N° {EMISOR_RUC}").Bold().FontSize(13).FontColor("#1E3A8A");
                            col.Item().AlignCenter().Text("BOLETA DE VENTA ELECTRÓNICA").Bold().FontSize(11).FontColor("#1E3A8A");
                            col.Item().AlignCenter().Text(comprobanteFormateado).Bold().FontSize(12).FontColor("#0F172A");
                        });
                    });

                    // Cuerpo principal del documento
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Datos del Adquirente y la Transacción
                        col.Item().Border(1).BorderColor("#CBD5E1").Background("#F1F5F9").Padding(8).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Señor(es): ").Bold(); t.Span(clienteNombre); });
                                c.Item().Text(t => { t.Span("Doc. Identidad: ").Bold(); t.Span(clienteDoc != "Sin Documento" ? $"{tipoDocClienteStr} {clienteDoc}" : "Sin Documento (Venta Mostrador)"); });
                                c.Item().Text(t => { t.Span("Forma de Pago: ").Bold(); t.Span(venta.TipoVenta == TipoVentaConsts.Credito ? "💳 A CRÉDITO (Fiado Registrado)" : "💵 AL CONTADO"); });
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Fecha de Emisión: ").Bold(); t.Span(fechaStr); });
                                c.Item().Text(t => { t.Span("Hora de Emisión: ").Bold(); t.Span(horaStr); });
                                c.Item().Text(t => { t.Span("Moneda: ").Bold(); t.Span("SOLES (S/)"); });
                                c.Item().Text(t => { t.Span("Cajero / Vendedor: ").Bold(); t.Span(venta.UsuarioVendedor ?? "Caja Principal"); });
                            });
                        });

                        col.Item().Height(8);

                        // Tabla Profesional de Productos Vendidos
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(24);  // #
                                columns.ConstantColumn(65);  // Código
                                columns.RelativeColumn(3);   // Descripción
                                columns.ConstantColumn(55);  // Cantidad
                                columns.ConstantColumn(55);  // U.Med.
                                columns.ConstantColumn(70);  // P.Unit
                                columns.ConstantColumn(75);  // Subtotal
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#1E3A8A").Padding(5).Text("#").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).Text("Código").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).Text("Descripción del Producto / Material").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("Cant.").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).Text("U.Med").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("P.Unit (S/)").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("Importe (S/)").Bold().FontColor("#FFFFFF").FontSize(8.5f);
                            });

                            int idx = 1;
                            if (venta.Detalles != null)
                            {
                                foreach (var item in venta.Detalles)
                                {
                                    string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                                    string codStr = item.Producto?.Codigo ?? (item.ProductoId > 0 ? $"PRD-{item.ProductoId:D3}" : "-");
                                    string umStr = string.IsNullOrWhiteSpace(item.UnidadMedida) ? "Unidad" : item.UnidadMedida;

                                    table.Cell().Background(bg).Padding(4).Text(idx.ToString()).FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).Text(codStr).FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).Text(item.ProductoNombre).FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).AlignRight().Text(item.Cantidad.ToString("0.##")).FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).Text(umStr).FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).AlignRight().Text($"S/ {item.PrecioUnitario:N2}").FontSize(8.5f);
                                    table.Cell().Background(bg).Padding(4).AlignRight().Text($"S/ {item.Subtotal:N2}").Bold().FontSize(8.5f);
                                    idx++;
                                }
                            }
                        });

                        col.Item().Height(8);

                        // Totales Resumen Tributario SUNAT + Monto en Letras + QR Code
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"SON: {ConvertirMontoATexto(total)}").Bold().FontSize(8.5f).FontColor("#1E2514");
                                c.Item().Height(6);

                                if (venta.FueModificada)
                                {
                                    c.Item().Background("#FEF3C7").Padding(4).Text("⚠️ Comprobante ajustado por Auditoría Gerencial de Venta").Bold().FontSize(8).FontColor("#B45309");
                                    c.Item().Height(4);
                                }

                                c.Item().Row(qrRow =>
                                {
                                    qrRow.ConstantItem(80).Width(80).Height(80).Image(qrBytes);
                                    qrRow.RelativeItem().PaddingLeft(6).Column(qrCol =>
                                    {
                                        qrCol.Item().Text("Código QR de Representación Impresa").Bold().FontSize(8).FontColor("#1E3A8A");
                                        qrCol.Item().Text($"Resumen Hash: SUNAT-{venta.Fecha:yyyyMMdd}-{venta.Id:D6}").FontSize(7.5f).FontColor("#64748B");
                                        qrCol.Item().Text("Consulte su validez en la sede electrónica de SUNAT.").FontSize(7.5f).FontColor("#64748B");
                                    });
                                });
                            });

                            r.ConstantItem(210).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(6).Column(c =>
                            {
                                c.Item().Row(tr => { tr.RelativeItem().Text("OP. GRAVADA:"); tr.ConstantItem(85).AlignRight().Text($"S/ {opGravada:N2}"); });
                                c.Item().Row(tr => { tr.RelativeItem().Text("OP. EXONERADA:"); tr.ConstantItem(85).AlignRight().Text("S/ 0.00"); });
                                c.Item().Row(tr => { tr.RelativeItem().Text("OP. INAFECTA:"); tr.ConstantItem(85).AlignRight().Text("S/ 0.00"); });
                                c.Item().Row(tr => { tr.RelativeItem().Text("I.G.V. (18%):"); tr.ConstantItem(85).AlignRight().Text($"S/ {igv:N2}"); });
                                c.Item().LineHorizontal(0.5f).LineColor("#94A3B8");
                                c.Item().Row(tr => { tr.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(11).FontColor("#1E3A8A"); tr.ConstantItem(85).AlignRight().Text($"S/ {total:N2}").Bold().FontSize(12).FontColor("#1E3A8A"); });
                            });
                        });
                    });

                    // Pie de Página Oficial
                    page.Footer().AlignCenter().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f).LineColor("#CBD5E1");
                        c.Item().PaddingTop(3).Text("Representación impresa de la BOLETA DE VENTA ELECTRÓNICA, generada desde el sistema de gestión ACEMAR. • ¡Gracias por su compra!").FontSize(7.5f).FontColor("#64748B");
                    });
                });
            });

            return doc.GeneratePdf();
        }

        // ── 2. GENERACIÓN DE TICKET TÉRMICO PDF (FORMATO ANCHO 80MM) ───────────────
        public byte[] GenerarPdfTicketTermico(Venta venta)
        {
            var (serie, correlativo, comprobanteFormateado) = ObtenerSerieYCorrelativo(venta);
            string fechaStr = venta.Fecha.ToString("dd/MM/yyyy");
            string horaStr = venta.Fecha.ToString("HH:mm:ss");

            decimal total = venta.Total;
            decimal opGravada = Math.Round(total / 1.18m, 2);
            decimal igv = Math.Round(total - opGravada, 2);

            string clienteNombre = string.IsNullOrWhiteSpace(venta.ClienteNombre) ? "Cliente Mostrador" : venta.ClienteNombre.Trim();
            string clienteDoc = venta.ClienteCredito?.Documento ?? "Sin Documento";

            string qrSunatData = $"{EMISOR_RUC}|03|{serie}|{correlativo}|{igv:F2}|{total:F2}|{venta.Fecha:yyyy-MM-dd}|{(clienteDoc != "Sin Documento" ? "1" : "-")}|{clienteDoc}|";
            byte[] qrBytes = QrCodeHelper.GenerarQrCodeBytes(qrSunatData);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(80, 220, Unit.Millimetre);
                    page.Margin(3, Unit.Millimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Segoe UI"));

                    page.Content().Column(col =>
                    {
                        // Encabezado Térmico
                        col.Item().AlignCenter().Text(EMISOR_NOMBRE_COMERCIAL).Bold().FontSize(12).FontColor("#000000");
                        col.Item().AlignCenter().Text(EMISOR_RAZON_SOCIAL).FontSize(7.5f);
                        col.Item().AlignCenter().Text($"R.U.C. N° {EMISOR_RUC}").Bold().FontSize(9);
                        col.Item().AlignCenter().Text(EMISOR_DIRECCION).FontSize(7);
                        col.Item().AlignCenter().Text("Teléfono: 94301837").FontSize(7);
                        col.Item().LineHorizontal(0.5f).LineColor("#000000");

                        // Datos del Comprobante
                        col.Item().AlignCenter().Text("BOLETA DE VENTA ELECTRÓNICA").Bold().FontSize(9);
                        col.Item().AlignCenter().Text(comprobanteFormateado).Bold().FontSize(10);
                        col.Item().LineHorizontal(0.5f);

                        col.Item().Text($"Fecha: {fechaStr}  Hora: {horaStr}");
                        col.Item().Text($"Cajero: {venta.UsuarioVendedor ?? "Caja Central"}");
                        col.Item().Text($"Cliente: {clienteNombre}");
                        if (clienteDoc != "Sin Documento")
                        {
                            col.Item().Text($"Doc. Cliente: {clienteDoc}");
                        }
                        col.Item().Text($"Forma de Pago: {(venta.TipoVenta == TipoVentaConsts.Credito ? "A CRÉDITO" : "AL CONTADO")}");
                        col.Item().LineHorizontal(0.5f);

                        // Ítems de la Venta (Optimizado para Papel Térmico)
                        if (venta.Detalles != null)
                        {
                            foreach (var item in venta.Detalles)
                            {
                                string codStr = item.Producto?.Codigo ?? "";
                                string nombreConCod = !string.IsNullOrWhiteSpace(codStr) ? $"[{codStr}] {item.ProductoNombre}" : item.ProductoNombre;
                                string umStr = string.IsNullOrWhiteSpace(item.UnidadMedida) ? "U." : item.UnidadMedida;

                                col.Item().Text(nombreConCod).Bold().FontSize(8);
                                col.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"  {item.Cantidad:0.##} {umStr} x S/{item.PrecioUnitario:N2}");
                                    r.ConstantItem(50).AlignRight().Text($"S/ {item.Subtotal:N2}").Bold();
                                });
                            }
                        }

                        col.Item().LineHorizontal(0.5f);

                        // Totales Tributarios SUNAT
                        col.Item().Row(r => { r.RelativeItem().Text("OP. GRAVADA:"); r.ConstantItem(55).AlignRight().Text($"S/ {opGravada:N2}"); });
                        col.Item().Row(r => { r.RelativeItem().Text("I.G.V. (18%):"); r.ConstantItem(55).AlignRight().Text($"S/ {igv:N2}"); });
                        col.Item().LineHorizontal(0.5f);

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(10);
                            r.ConstantItem(60).AlignRight().Text($"S/ {total:N2}").Bold().FontSize(11);
                        });

                        col.Item().Text($"SON: {ConvertirMontoATexto(total)}").FontSize(7).Italic();
                        col.Item().LineHorizontal(0.5f);

                        // QR Code y Leyenda SUNAT
                        col.Item().AlignCenter().Width(65).Height(65).Image(qrBytes);
                        col.Item().AlignCenter().Text("Representación impresa de la").FontSize(6.5f);
                        col.Item().AlignCenter().Text("Boleta de Venta Electrónica.").Bold().FontSize(7);
                        col.Item().AlignCenter().Text("¡Gracias por su preferencia en ACEMAR!").FontSize(7.5f).Bold();
                    });
                });
            });

            return doc.GeneratePdf();
        }

        // ── 3. GENERACIÓN DE PROFORMA COMERCIAL EN PDF ───────────────────────────
        public byte[] GenerarPdfProforma(Proforma proforma)
        {
            decimal total = proforma.Total;
            decimal opGravada = Math.Round(total / 1.18m, 2);
            decimal igv = Math.Round(total - opGravada, 2);

            string clienteNombre = string.IsNullOrWhiteSpace(proforma.ClienteNombre) ? "Cliente Mostrador" : proforma.ClienteNombre.Trim();
            string clienteDoc = string.IsNullOrWhiteSpace(proforma.ClienteDocumento) ? "Sin Documento" : proforma.ClienteDocumento.Trim();
            string numProformaStr = string.IsNullOrWhiteSpace(proforma.NumeroProforma) ? $"PRO-{proforma.Id:D6}" : proforma.NumeroProforma;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Segoe UI"));

                    // Encabezado Corporativo Institucional ACEMAR
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(EMISOR_NOMBRE_COMERCIAL).Bold().FontSize(16).FontColor("#1E3A8A");
                            col.Item().Text(EMISOR_RAZON_SOCIAL).FontSize(10).Bold().FontColor("#334155");
                            col.Item().Text($"R.U.C. N° {EMISOR_RUC}").FontSize(9.5f).FontColor("#475569");
                            col.Item().Text(EMISOR_DIRECCION).FontSize(9).FontColor("#64748B");
                            col.Item().Text($"Teléfono: 94301837 • Correo: ventas@acemar.pe").FontSize(8.5f).FontColor("#64748B");
                        });

                        row.ConstantItem(220).Border(1.5f).BorderColor("#1E3A8A").Background("#F8FAFC").Padding(8).Column(col =>
                        {
                            col.Item().AlignCenter().Text($"R.U.C. N° {EMISOR_RUC}").Bold().FontSize(12).FontColor("#1E3A8A");
                            col.Item().AlignCenter().Text("PROFORMA COMERCIAL").Bold().FontSize(11).FontColor("#1E3A8A");
                            col.Item().AlignCenter().Text(numProformaStr).Bold().FontSize(13).FontColor("#0F172A");
                        });
                    });

                    // Contenido Principal
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor("#1E3A8A");
                        col.Item().Height(6);

                        // Cuadro de Datos del Cliente y Cotización
                        col.Item().Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(8).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Cliente / Adquirente: ").Bold().FontColor("#1E3A8A"); t.Span(clienteNombre); });
                                c.Item().Text(t => { t.Span("Documento Identidad: ").Bold().FontColor("#1E3A8A"); t.Span(clienteDoc); });
                                c.Item().Text(t => { t.Span("Moneda de Cotización: ").Bold().FontColor("#1E3A8A"); t.Span("SOLES (S/)"); });
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Fecha de Emisión: ").Bold().FontColor("#1E3A8A"); t.Span(proforma.Fecha.ToString("dd/MM/yyyy HH:mm:ss")); });
                                c.Item().Text(t => { t.Span("Asesor Comercial: ").Bold().FontColor("#1E3A8A"); t.Span(string.IsNullOrWhiteSpace(proforma.Usuario) ? "Vendedor Mostrador" : proforma.Usuario); });
                                c.Item().Text(t => { t.Span("Validez Cotización: ").Bold().FontColor("#1E3A8A"); t.Span("15 días calendario"); });
                            });
                        });

                        col.Item().Height(10);

                        // Tabla Estilizada de Productos Cotizados
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(65);
                                columns.ConstantColumn(65);
                                columns.ConstantColumn(75);
                                columns.ConstantColumn(85);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#1E3A8A").Padding(5).Text("#").Bold().FontColor("#FFFFFF");
                                header.Cell().Background("#1E3A8A").Padding(5).Text("DESCRIPCIÓN DE PRODUCTO / MATERIAL").Bold().FontColor("#FFFFFF");
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("CANT.").Bold().FontColor("#FFFFFF");
                                header.Cell().Background("#1E3A8A").Padding(5).Text("UNIDAD").Bold().FontColor("#FFFFFF");
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("P. UNIT (S/)").Bold().FontColor("#FFFFFF");
                                header.Cell().Background("#1E3A8A").Padding(5).AlignRight().Text("IMPORTE (S/)").Bold().FontColor("#FFFFFF");
                            });

                            int idx = 1;
                            if (proforma.Detalles != null)
                            {
                                foreach (var item in proforma.Detalles)
                                {
                                    string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                                    table.Cell().Background(bg).Padding(5).Text(idx.ToString());
                                    table.Cell().Background(bg).Padding(5).Text(item.ProductoNombre);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text(item.Cantidad.ToString("0.##"));
                                    table.Cell().Background(bg).Padding(5).Text(string.IsNullOrWhiteSpace(item.UnidadMedida) ? "Unidad" : item.UnidadMedida);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"S/ {item.PrecioUnitario:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"S/ {item.Subtotal:N2}").Bold();
                                    idx++;
                                }
                            }
                        });

                        col.Item().Height(10);

                        // Monto en Letras y Desglose Tributario Proforma
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"SON: {ConvertirMontoATexto(total)}").Bold().FontSize(8.5f).FontColor("#1E2514");
                                c.Item().Height(8);
                                c.Item().Text("• Precios de cotización válidos por 15 días sujetos a disponibilidad de almacén.").Italic().FontSize(8).FontColor("#64748B");
                                c.Item().Text("• Este documento no tiene valor tributario ni fiscal hasta su conversión a Boleta.").Italic().FontSize(8).FontColor("#64748B");
                            });

                            r.ConstantItem(210).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(6).Column(c =>
                            {
                                c.Item().Row(tr => { tr.RelativeItem().Text("OP. GRAVADA ESTIMADA:"); tr.ConstantItem(85).AlignRight().Text($"S/ {opGravada:N2}"); });
                                c.Item().Row(tr => { tr.RelativeItem().Text("I.G.V. ESTIMADO (18%):"); tr.ConstantItem(85).AlignRight().Text($"S/ {igv:N2}"); });
                                c.Item().LineHorizontal(0.5f).LineColor("#94A3B8");
                                c.Item().Row(tr => { tr.RelativeItem().Text("TOTAL PROFORMA:").Bold().FontSize(11).FontColor("#1E3A8A"); tr.ConstantItem(85).AlignRight().Text($"S/ {total:N2}").Bold().FontSize(12).FontColor("#1E3A8A"); });
                            });
                        });
                    });

                    // Pie de Página
                    page.Footer().AlignCenter().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f).LineColor("#CBD5E1");
                        c.Item().PaddingTop(3).Text("Proforma Comercial generada desde el sistema de gestión ACEMAR. • ¡Gracias por su preferencia!").FontSize(7.5f).FontColor("#64748B");
                    });
                });
            });

            return doc.GeneratePdf();
        }

        public byte[] GenerarPdfReporteStock(List<Producto> productos, List<LoteProducto> lotes)
        {
            var html = GenerarHtmlReporteStock(productos, lotes);
            return Encoding.UTF8.GetBytes(html);
        }

        // ── 4. GUARDAR PDF EN DISCO ───────────────────────────────────────────────
        public string GuardarPdf(byte[] pdfBytes, string subfolder, string nombreArchivo)
        {
            if (!nombreArchivo.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                nombreArchivo += ".pdf";
            }

            string folder = Path.Combine(_documentFolder, subfolder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string fullPath = Path.Combine(folder, nombreArchivo);
            File.WriteAllBytes(fullPath, pdfBytes);
            return fullPath;
        }

        // ── 5. GUARDAR Y ABRIR EN VISOR DEL SISTEMA ──────────────────────────────
        public string GuardarYAbrirPdf(byte[] pdfBytes, string subfolder, string nombreArchivo)
        {
            string fullPath = GuardarPdf(pdfBytes, subfolder, nombreArchivo);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }
            catch { }

            return fullPath;
        }

        // ── 6. MÉTODOS HTML HISTÓRICOS Y DE RESPALDO ──────────────────────────────
        public string GenerarHtmlProforma(Proforma proforma)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Proforma ACEMAR</title></head><body>");
            sb.AppendLine($"<h2>Proforma {proforma.NumeroProforma} - {proforma.ClienteNombre}</h2>");
            sb.AppendLine($"<p>Total: S/ {proforma.Total:N2}</p></body></html>");
            return sb.ToString();
        }

        public string GenerarHtmlVenta(Venta venta)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Boleta ACEMAR</title></head><body>");
            sb.AppendLine($"<h2>Venta Ticket: {venta.NumeroTicket} - Total: S/ {venta.Total:N2}</h2></body></html>");
            return sb.ToString();
        }

        public string GenerarHtmlReporteStock(List<Producto> productos, List<LoteProducto> lotes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Reporte Inventario</title></head><body>");
            sb.AppendLine($"<h2>Reporte de Stock ACEMAR ({DateTime.Now:dd/MM/yyyy})</h2></body></html>");
            return sb.ToString();
        }

        public async Task<string> GuardarDocumentoHtmlAsync(string htmlContent, string subfolder, string fileName)
        {
            string folder = Path.Combine(_documentFolder, subfolder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string fullPath = Path.Combine(folder, fileName);
            await File.WriteAllTextAsync(fullPath, htmlContent, Encoding.UTF8);
            return fullPath;
        }

        // ── 7. MÉTODOS AUXILIARES Y FORMATO DE TEXTO ─────────────────────────────

        private static (string Serie, string Correlativo, string Formateado) ObtenerSerieYCorrelativo(Venta venta)
        {
            string serie = "B001";
            string correlativo = venta.Id > 0 ? venta.Id.ToString("D8") : "00000001";

            if (!string.IsNullOrWhiteSpace(venta.NumeroTicket))
            {
                var partes = venta.NumeroTicket.Split('-');
                if (partes.Length >= 2 && partes[0].StartsWith("B", StringComparison.OrdinalIgnoreCase))
                {
                    serie = partes[0].ToUpper();
                    correlativo = partes[1].PadLeft(8, '0');
                }
                else if (partes.Length >= 3 && int.TryParse(partes[2], out int idNum))
                {
                    correlativo = idNum.ToString("D8");
                }
            }

            return (serie, correlativo, $"{serie}-{correlativo}");
        }

        private static string ConvertirMontoATexto(decimal monto)
        {
            long enteros = (long)Math.Truncate(monto);
            int centavos = (int)Math.Round((monto - enteros) * 100);

            if (enteros == 0) return $"CERO CON {centavos:D2}/100 SOLES";

            string textoEnteros = NumeroATexto(enteros);
            return $"{textoEnteros} CON {centavos:D2}/100 SOLES";
        }

        private static string NumeroATexto(long n)
        {
            if (n == 0) return "CERO";
            if (n < 0) return "MENOS " + NumeroATexto(Math.Abs(n));

            if (n == 1) return "UN";
            if (n == 2) return "DOS";
            if (n == 3) return "TRES";
            if (n == 4) return "CUATRO";
            if (n == 5) return "CINCO";
            if (n == 6) return "SEIS";
            if (n == 7) return "SIETE";
            if (n == 8) return "OCHO";
            if (n == 9) return "NUEVE";
            if (n == 10) return "DIEZ";
            if (n == 11) return "ONCE";
            if (n == 12) return "DOCE";
            if (n == 13) return "TRECE";
            if (n == 14) return "CATORCE";
            if (n == 15) return "QUINCE";
            if (n < 20) return "DIECI" + NumeroATexto(n - 10);
            if (n == 20) return "VEINTE";
            if (n < 30) return "VEINTI" + NumeroATexto(n - 20);
            if (n == 30) return "TREINTA";
            if (n < 40) return "TREINTA Y " + NumeroATexto(n - 30);
            if (n == 40) return "CUARENTA";
            if (n < 50) return "CUARENTA Y " + NumeroATexto(n - 40);
            if (n == 50) return "CINCUENTA";
            if (n < 60) return "CINCUENTA Y " + NumeroATexto(n - 50);
            if (n == 60) return "SESENTA";
            if (n < 70) return "SESENTA Y " + NumeroATexto(n - 60);
            if (n == 70) return "SETENTA";
            if (n < 80) return "SETENTA Y " + NumeroATexto(n - 70);
            if (n == 80) return "OCHENTA";
            if (n < 90) return "OCHENTA Y " + NumeroATexto(n - 80);
            if (n == 90) return "NOVENTA";
            if (n < 100) return "NOVENTA Y " + NumeroATexto(n - 90);
            if (n == 100) return "CIEN";
            if (n < 200) return "CIENTO " + NumeroATexto(n - 100);
            if (n < 300) return "DOSCIENTOS " + NumeroATexto(n - 200);
            if (n < 400) return "TRESCIENTOS " + NumeroATexto(n - 300);
            if (n < 500) return "CUATROCIENTOS " + NumeroATexto(n - 400);
            if (n < 600) return "QUINIENTOS " + NumeroATexto(n - 500);
            if (n < 700) return "SEISCIENTOS " + NumeroATexto(n - 600);
            if (n < 800) return "SETECIENTOS " + NumeroATexto(n - 700);
            if (n < 900) return "OCHOCIENTOS " + NumeroATexto(n - 800);
            if (n < 1000) return "NOVECIENTOS " + NumeroATexto(n - 900);
            if (n == 1000) return "MIL";
            if (n < 2000) return "MIL " + NumeroATexto(n - 1000);
            if (n < 1000000)
            {
                long miles = n / 1000;
                long resto = n % 1000;
                string textoMiles = miles == 1 ? "MIL" : NumeroATexto(miles) + " MIL";
                return resto > 0 ? $"{textoMiles} {NumeroATexto(resto)}" : textoMiles;
            }
            if (n == 1000000) return "UN MILLÓN";
            if (n < 2000000) return "UN MILLÓN " + NumeroATexto(n - 1000000);

            long millones = n / 1000000;
            long restoMillones = n % 1000000;
            string textoMillones = NumeroATexto(millones) + " MILLONES";
            return restoMillones > 0 ? $"{textoMillones} {NumeroATexto(restoMillones)}" : textoMillones;
        }
    }
}
