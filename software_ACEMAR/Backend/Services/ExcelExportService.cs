using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IExcelExportService
    {
        bool ExportarResumenCaja(ResumenCaja resumen, string tituloPeriodo, string usuario, string rutaArchivo, out string error);
        bool ExportarResumenCajaACsv(ResumenCaja resumen, string tituloPeriodo, string usuario, string rutaArchivo, out string error);
    }

    public class ExcelExportService : IExcelExportService
    {
        public bool ExportarResumenCaja(ResumenCaja resumen, string tituloPeriodo, string usuario, string rutaArchivo, out string error)
        {
            error = string.Empty;
            try
            {
                if (rutaArchivo.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return ExportarResumenCajaACsv(resumen, tituloPeriodo, usuario, rutaArchivo, out error);
                }

                var inicio = resumen.Fecha.Date;
                var fin = (resumen.FechaHasta ?? resumen.Fecha).Date.AddDays(1);

                List<Venta> ventas = new();
                List<LoteProducto> lotes = new();

                try
                {
                    using var context = new AppDbContext();
                    ventas = context.Ventas
                        .AsNoTracking()
                        .Include(v => v.Detalles)
                        .Where(v => v.Fecha >= inicio && v.Fecha < fin)
                        .OrderByDescending(v => v.Fecha)
                        .ToList();

                    lotes = context.LotesProducto
                        .AsNoTracking()
                        .Include(l => l.Producto)
                        .Include(l => l.Proveedor)
                        .Where(l => l.FechaIngreso >= inicio && l.FechaIngreso < fin)
                        .OrderByDescending(l => l.FechaIngreso)
                        .ToList();
                }
                catch
                {
                    // Fallback en caso de que no se cargue DB
                }

                using var workbook = new XLWorkbook();

                // -------------------------------------------------------------
                // HOJA 1: RESUMEN CONTABLE Y FINANCIERO
                // -------------------------------------------------------------
                var wsResumen = workbook.Worksheets.Add("RESUMEN CONTABLE");
                wsResumen.ShowGridLines = true;

                // Banner Superior Institucional
                wsResumen.Range("A1:C1").Merge();
                wsResumen.Cell("A1").Value = "FERRETERÍA & ALMACÉN ACEMAR S.A.C.";
                wsResumen.Cell("A1").Style.Font.Bold = true;
                wsResumen.Cell("A1").Style.Font.FontSize = 16;
                wsResumen.Cell("A1").Style.Font.FontColor = XLColor.White;
                wsResumen.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                wsResumen.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsResumen.Row(1).Height = 28;

                wsResumen.Range("A2:C2").Merge();
                wsResumen.Cell("A2").Value = "REPORTE DE CONTABILIDAD Y BALANCE DE CIERRE DE CAJA";
                wsResumen.Cell("A2").Style.Font.Bold = true;
                wsResumen.Cell("A2").Style.Font.FontSize = 11;
                wsResumen.Cell("A2").Style.Font.FontColor = XLColor.White;
                wsResumen.Cell("A2").Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                wsResumen.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsResumen.Row(2).Height = 20;

                // Bloque de Metadatos
                int row = 4;
                wsResumen.Cell(row, 1).Value = "Período Evaluado:";
                wsResumen.Cell(row, 1).Style.Font.Bold = true;
                wsResumen.Cell(row, 2).Value = tituloPeriodo;

                row++;
                wsResumen.Cell(row, 1).Value = "Rango de Fechas:";
                wsResumen.Cell(row, 1).Style.Font.Bold = true;
                string fechaHastaStr = resumen.FechaHasta.HasValue ? $" al {resumen.FechaHasta.Value:dd/MM/yyyy}" : "";
                wsResumen.Cell(row, 2).Value = $"{resumen.Fecha:dd/MM/yyyy}{fechaHastaStr}";

                row++;
                wsResumen.Cell(row, 1).Value = "Fecha de Generación:";
                wsResumen.Cell(row, 1).Style.Font.Bold = true;
                wsResumen.Cell(row, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                row++;
                wsResumen.Cell(row, 1).Value = "Usuario Operador:";
                wsResumen.Cell(row, 1).Style.Font.Bold = true;
                wsResumen.Cell(row, 2).Value = usuario;

                // Tabla de Balance Contable
                row += 2;
                wsResumen.Cell(row, 1).Value = "CONCEPTO CONTABLE";
                wsResumen.Cell(row, 2).Value = "MONTO (S/)";
                wsResumen.Cell(row, 3).Value = "NATURALEZA / OBSERVACIÓN";

                var headerRange = wsResumen.Range(row, 1, row, 3);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsResumen.Row(row).Height = 22;

                var itemsResumen = new List<(string Concepto, decimal Monto, string Nat, bool Highlight, string BgColor)>
                {
                    ("Fondo / Monto de Apertura en Caja", resumen.MontoApertura, "Base de Caja", false, "#FFFFFF"),
                    ("(+) Ventas al Contado (Efectivo Mostrador)", resumen.TotalVentasEfectivo, "Ingreso Físico", false, "#F8FAFC"),
                    ("(+) Ventas Digitales (Yape / Plin / Transferencia)", resumen.TotalVentasDigital, "Ingreso BANC / APP", false, "#F8FAFC"),
                    ("(+) Abonos de Créditos Cobrados en Efectivo", resumen.TotalAbonosEfectivo, "Ingreso Físico", false, "#F8FAFC"),
                    ("(=) TOTAL INGRESOS GENERALES COBRADOS", resumen.TotalVentasEfectivo + resumen.TotalVentasDigital + resumen.TotalAbonosEfectivo, "SUBTOTAL INGRESOS", true, "#DCFCE7"),
                    ("(-) Ventas a Crédito (Fiados Pendientes)", resumen.TotalVentasCredito, "Por Cobrar / Deuda", false, "#FFF7ED"),
                    ("(-) Inversión en Compras de Nuevos Lotes", resumen.TotalGastosLotes, "Egreso Stock", false, "#FEF2F2"),
                    ("(-) Costo de Ventas (COGS - Costo Compra Productos)", resumen.TotalCostoVentas, "Costo Operativo", false, "#F1F5F9"),
                    ("(=) UTILIDAD BRUTA ESTIMADA DE VENTAS", resumen.UtilidadBruta, "BALANCE GANANCIA", true, "#DBEAFE"),
                    ("(=) BALANCE FINAL GENERAL EN CAJA FISICA", resumen.TotalEfectivoCaja, "TOTAL CAJA CHICA", true, "#FEF3C7")
                };

                foreach (var item in itemsResumen)
                {
                    row++;
                    wsResumen.Cell(row, 1).Value = item.Concepto;
                    wsResumen.Cell(row, 2).Value = item.Monto;
                    wsResumen.Cell(row, 2).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                    wsResumen.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    wsResumen.Cell(row, 3).Value = item.Nat;

                    var rowRange = wsResumen.Range(row, 1, row, 3);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml(item.BgColor);
                    rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

                    if (item.Highlight)
                    {
                        rowRange.Style.Font.Bold = true;
                        wsResumen.Cell(row, 1).Style.Font.FontSize = 11;
                    }
                }

                wsResumen.Columns().AdjustToContents();

                // -------------------------------------------------------------
                // HOJA 2: REGISTRO DE VENTAS
                // -------------------------------------------------------------
                var wsVentas = workbook.Worksheets.Add("VENTAS");
                wsVentas.ShowGridLines = true;

                wsVentas.Cell("A1").Value = "REGISTRO DETALLADO DE VENTAS DEL PERÍODO";
                wsVentas.Cell("A1").Style.Font.Bold = true;
                wsVentas.Cell("A1").Style.Font.FontSize = 14;

                string[] headersVentas = { "N.º TICKET/SERIE", "FECHA Y HORA", "TIPO VENTA / PAGO", "CLIENTE", "VENDEDOR", "SUBTOTAL (S/)", "IGV 18% (S/)", "TOTAL VENTA (S/)", "ESTADO" };
                for (int c = 0; c < headersVentas.Length; c++)
                {
                    var cell = wsVentas.Cell(3, c + 1);
                    cell.Value = headersVentas[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                wsVentas.Row(3).Height = 22;

                int rV = 4;
                if (ventas.Any())
                {
                    foreach (var v in ventas)
                    {
                        decimal subtotalV = Math.Round(v.Total / 1.18m, 2);
                        decimal igvV = Math.Round(v.Total - subtotalV, 2);

                        wsVentas.Cell(rV, 1).Value = v.NumeroTicket;
                        wsVentas.Cell(rV, 2).Value = v.Fecha;
                        wsVentas.Cell(rV, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                        wsVentas.Cell(rV, 3).Value = v.TipoVenta;
                        wsVentas.Cell(rV, 4).Value = v.ClienteNombre ?? "Clientes Varios";
                        wsVentas.Cell(rV, 5).Value = v.UsuarioVendedor ?? "Vendedor Mostrador";

                        wsVentas.Cell(rV, 6).Value = subtotalV;
                        wsVentas.Cell(rV, 6).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        wsVentas.Cell(rV, 7).Value = igvV;
                        wsVentas.Cell(rV, 7).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        wsVentas.Cell(rV, 8).Value = v.Total;
                        wsVentas.Cell(rV, 8).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        wsVentas.Cell(rV, 9).Value = v.FueModificada ? "Modificada" : "Normal";

                        rV++;
                    }

                    // Fila de Totales con fórmula Excel
                    wsVentas.Cell(rV, 1).Value = "TOTALES DE VENTAS";
                    wsVentas.Cell(rV, 1).Style.Font.Bold = true;

                    wsVentas.Cell(rV, 6).FormulaA1 = $"SUM(F4:F{rV - 1})";
                    wsVentas.Cell(rV, 6).Style.Font.Bold = true;
                    wsVentas.Cell(rV, 6).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    wsVentas.Cell(rV, 7).FormulaA1 = $"SUM(G4:G{rV - 1})";
                    wsVentas.Cell(rV, 7).Style.Font.Bold = true;
                    wsVentas.Cell(rV, 7).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    wsVentas.Cell(rV, 8).FormulaA1 = $"SUM(H4:H{rV - 1})";
                    wsVentas.Cell(rV, 8).Style.Font.Bold = true;
                    wsVentas.Cell(rV, 8).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    var totalRange = wsVentas.Range(rV, 1, rV, 9);
                    totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                    totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                }

                wsVentas.SheetView.FreezeRows(3);
                wsVentas.Columns().AdjustToContents();

                // -------------------------------------------------------------
                // HOJA 3: DETALLE DE PRODUCTOS VENDIDOS
                // -------------------------------------------------------------
                var wsDetalles = workbook.Worksheets.Add("DETALLE DE PRODUCTOS");
                wsDetalles.ShowGridLines = true;

                wsDetalles.Cell("A1").Value = "DETALLE LÍNEA POR LÍNEA DE PRODUCTOS VENDIDOS";
                wsDetalles.Cell("A1").Style.Font.Bold = true;
                wsDetalles.Cell("A1").Style.Font.FontSize = 14;

                string[] headersDetalles = { "N.º TICKET", "FECHA VENTA", "DESCRIPCIÓN DEL PRODUCTO", "CANTIDAD", "UNIDAD", "P. UNITARIO (S/)", "IMPORTE LÍNEA (S/)" };
                for (int c = 0; c < headersDetalles.Length; c++)
                {
                    var cell = wsDetalles.Cell(3, c + 1);
                    cell.Value = headersDetalles[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                wsDetalles.Row(3).Height = 22;

                int rD = 4;
                foreach (var v in ventas)
                {
                    if (v.Detalles != null)
                    {
                        foreach (var d in v.Detalles)
                        {
                            wsDetalles.Cell(rD, 1).Value = v.NumeroTicket;
                            wsDetalles.Cell(rD, 2).Value = v.Fecha;
                            wsDetalles.Cell(rD, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                            wsDetalles.Cell(rD, 3).Value = d.ProductoNombre;
                            wsDetalles.Cell(rD, 4).Value = d.Cantidad;
                            wsDetalles.Cell(rD, 4).Style.NumberFormat.Format = "#,##0.00";
                            wsDetalles.Cell(rD, 5).Value = string.IsNullOrWhiteSpace(d.UnidadMedida) ? "Unidades" : d.UnidadMedida;

                            wsDetalles.Cell(rD, 6).Value = d.PrecioUnitario;
                            wsDetalles.Cell(rD, 6).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                            wsDetalles.Cell(rD, 7).Value = d.Subtotal;
                            wsDetalles.Cell(rD, 7).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                            rD++;
                        }
                    }
                }

                if (rD > 4)
                {
                    wsDetalles.Cell(rD, 1).Value = "TOTAL GENERAL ÍTEMS";
                    wsDetalles.Cell(rD, 1).Style.Font.Bold = true;

                    wsDetalles.Cell(rD, 4).FormulaA1 = $"SUM(D4:D{rD - 1})";
                    wsDetalles.Cell(rD, 4).Style.Font.Bold = true;
                    wsDetalles.Cell(rD, 4).Style.NumberFormat.Format = "#,##0.00";

                    wsDetalles.Cell(rD, 7).FormulaA1 = $"SUM(G4:G{rD - 1})";
                    wsDetalles.Cell(rD, 7).Style.Font.Bold = true;
                    wsDetalles.Cell(rD, 7).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    var totalRangeD = wsDetalles.Range(rD, 1, rD, 7);
                    totalRangeD.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                    totalRangeD.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRangeD.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                }

                wsDetalles.SheetView.FreezeRows(3);
                wsDetalles.Columns().AdjustToContents();

                // -------------------------------------------------------------
                // HOJA 4: COMPRAS Y LOTES DE PRODUCTOS
                // -------------------------------------------------------------
                var wsLotes = workbook.Worksheets.Add("COMPRAS Y LOTES");
                wsLotes.ShowGridLines = true;

                wsLotes.Cell("A1").Value = "REGISTRO DE INGRESO DE COMPRAS Y LOTES DE PRODUCTOS";
                wsLotes.Cell("A1").Style.Font.Bold = true;
                wsLotes.Cell("A1").Style.Font.FontSize = 14;

                string[] headersLotes = { "CÓDIGO LOTE", "FECHA INGRESO", "PRODUCTO", "PROVEEDOR", "CANT. INICIAL", "CANT. DISPONIBLE", "UNIDAD", "COSTO COMPRA UNIT (S/)", "COSTO TOTAL LOTE (S/)", "ESTADO" };
                for (int c = 0; c < headersLotes.Length; c++)
                {
                    var cell = wsLotes.Cell(3, c + 1);
                    cell.Value = headersLotes[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                wsLotes.Row(3).Height = 22;

                int rL = 4;
                if (lotes.Any())
                {
                    foreach (var l in lotes)
                    {
                        wsLotes.Cell(rL, 1).Value = $"LOT-{l.Id:D4}";
                        wsLotes.Cell(rL, 2).Value = l.FechaIngreso;
                        wsLotes.Cell(rL, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                        wsLotes.Cell(rL, 3).Value = l.Producto?.Nombre ?? $"Producto #{l.ProductoId}";
                        wsLotes.Cell(rL, 4).Value = l.ProveedorNombreDisplay;

                        wsLotes.Cell(rL, 5).Value = l.CantidadInicial;
                        wsLotes.Cell(rL, 5).Style.NumberFormat.Format = "#,##0.00";

                        wsLotes.Cell(rL, 6).Value = l.CantidadDisponible;
                        wsLotes.Cell(rL, 6).Style.NumberFormat.Format = "#,##0.00";

                        wsLotes.Cell(rL, 7).Value = string.IsNullOrWhiteSpace(l.UnidadMedida) ? "Unidades" : l.UnidadMedida;

                        wsLotes.Cell(rL, 8).Value = l.CostoCompra;
                        wsLotes.Cell(rL, 8).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        decimal costoTotalLote = l.CantidadInicial * l.CostoCompra;
                        wsLotes.Cell(rL, 9).Value = costoTotalLote;
                        wsLotes.Cell(rL, 9).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        wsLotes.Cell(rL, 10).Value = string.IsNullOrWhiteSpace(l.Estado) ? "Activo" : l.Estado;

                        rL++;
                    }

                    wsLotes.Cell(rL, 1).Value = "TOTAL INVERSIÓN EN LOTES";
                    wsLotes.Cell(rL, 1).Style.Font.Bold = true;

                    wsLotes.Cell(rL, 9).FormulaA1 = $"SUM(I4:I{rL - 1})";
                    wsLotes.Cell(rL, 9).Style.Font.Bold = true;
                    wsLotes.Cell(rL, 9).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    var totalRangeL = wsLotes.Range(rL, 1, rL, 10);
                    totalRangeL.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                    totalRangeL.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRangeL.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                }

                wsLotes.SheetView.FreezeRows(3);
                wsLotes.Columns().AdjustToContents();

                // -------------------------------------------------------------
                // HOJA 5: MOVIMIENTOS CRONOLÓGICOS DE CAJA
                // -------------------------------------------------------------
                var wsMov = workbook.Worksheets.Add("MOVIMIENTOS DE CAJA");
                wsMov.ShowGridLines = true;

                wsMov.Cell("A1").Value = "MOVIMIENTOS Y REGISTROS AUDITABLES DE CAJA";
                wsMov.Cell("A1").Style.Font.Bold = true;
                wsMov.Cell("A1").Style.Font.FontSize = 14;

                string[] headersMov = { "FECHA Y HORA", "TIPO MOVIMIENTO", "COMPROBANTE / DOC", "DETALLE / OBSERVACIONES", "NATURALEZA", "MONTO (S/)" };
                for (int c = 0; c < headersMov.Length; c++)
                {
                    var cell = wsMov.Cell(3, c + 1);
                    cell.Value = headersMov[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                wsMov.Row(3).Height = 22;

                int rM = 4;
                if (resumen.Movimientos != null && resumen.Movimientos.Any())
                {
                    foreach (var m in resumen.Movimientos)
                    {
                        wsMov.Cell(rM, 1).Value = m.FechaHora;
                        wsMov.Cell(rM, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";

                        wsMov.Cell(rM, 2).Value = m.Tipo;
                        wsMov.Cell(rM, 3).Value = m.Documento;
                        wsMov.Cell(rM, 4).Value = m.Detalle;
                        wsMov.Cell(rM, 5).Value = m.EsIngresoFisico ? "INGRESO" : ((m.Tipo != null && m.Tipo.Contains("Gasto")) ? "GASTO / EGRESO" : "CRÉDITO");

                        wsMov.Cell(rM, 6).Value = m.Monto;
                        wsMov.Cell(rM, 6).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                        rM++;
                    }
                }

                wsMov.SheetView.FreezeRows(3);
                wsMov.Columns().AdjustToContents();

                // Guardar archivo Excel .xlsx
                workbook.SaveAs(rutaArchivo);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error al generar el reporte Excel: {ex.Message}";
                return false;
            }
        }

        public bool ExportarResumenCajaACsv(ResumenCaja resumen, string tituloPeriodo, string usuario, string rutaArchivo, out string error)
        {
            error = string.Empty;
            try
            {
                var sb = new StringBuilder();

                // Encabezado principal del documento de Hoja de Cálculo
                sb.AppendLine("FERRETERÍA & ALMACÉN ACEMAR S.A.C.;REPORTE CONTABLE Y CIERRE DE CAJA EN HOJA DE CÁLCULO");
                sb.AppendLine($"Período de Evaluación;{tituloPeriodo}");
                sb.AppendLine($"Rango de Fechas;{resumen.Fecha:dd/MM/yyyy}{(resumen.FechaHasta.HasValue ? " al " + resumen.FechaHasta.Value.ToString("dd/MM/yyyy") : "")}");
                sb.AppendLine($"Fecha y Hora de Exportación;{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Usuario Operador;{usuario}");
                sb.AppendLine();

                // Sección 1: Cuadro Resumen Contable y Balance Ejecutivo
                sb.AppendLine("RESUMEN CONTABLE Y CONTABILIDAD GENERAL;MONTO (S/)");
                sb.AppendLine($"Fondo / Monto de Apertura en Caja;{resumen.MontoApertura:F2}");
                sb.AppendLine($"(+) Total Ventas en Efectivo Mostrador;{resumen.TotalVentasEfectivo:F2}");
                sb.AppendLine($"(+) Total Ventas Digitales (Yape / Plin / Transferencia);{resumen.TotalVentasDigital:F2}");
                sb.AppendLine($"(+) Total Abonos de Créditos Cobrados;{resumen.TotalAbonosEfectivo:F2}");
                sb.AppendLine($"(=) TOTAL INGRESOS GENERALES COBRADOS;{(resumen.TotalVentasEfectivo + resumen.TotalVentasDigital + resumen.TotalAbonosEfectivo):F2}");
                sb.AppendLine($"(-) Total Ventas a Crédito (Fiados Pendientes);{resumen.TotalVentasCredito:F2}");
                sb.AppendLine($"(-) Total Gastos por Inversión en Compras de Nuevos Lotes;{resumen.TotalGastosLotes:F2}");
                sb.AppendLine($"(-) Total Costo de Ventas (COGS / Costo Compra);{resumen.TotalCostoVentas:F2}");
                sb.AppendLine($"(=) UTILIDAD BRUTA ESTIMADA DE VENTAS;{resumen.UtilidadBruta:F2}");
                sb.AppendLine($"(=) BALANCE FINAL GENERAL EN CAJA;{resumen.TotalEfectivoCaja:F2}");
                sb.AppendLine();

                // Sección 2: Tabla Detallada Cronológica de Movimientos
                sb.AppendLine("DETALLE CRONOLÓGICO DE MOVIMIENTOS, REGISTROS Y VENTAS");
                sb.AppendLine("FECHA Y HORA;TIPO MOVIMIENTO;COMPROBANTE / DOC;DETALLE / OBSERVACIONES;NATURALEZA;MONTO (S/)");

                if (resumen.Movimientos != null)
                {
                    foreach (var m in resumen.Movimientos)
                    {
                        string naturaleza = m.EsIngresoFisico ? "INGRESO" : ((m.Tipo != null && m.Tipo.Contains("Gasto")) ? "GASTO / EGRESO" : "CRÉDITO");
                        string fechaStr = m.FechaHora.ToString("dd/MM/yyyy HH:mm:ss");
                        string detalleClean = m.Detalle?.Replace(";", "-").Replace("\r", " ").Replace("\n", " ") ?? "";
                        string tipoClean = m.Tipo?.Replace(";", "-") ?? "";

                        sb.AppendLine($"{fechaStr};{tipoClean};{m.Documento};{detalleClean};{naturaleza};{m.Monto:F2}");
                    }
                }

                // Guardar con BOM UTF-8 (\uFEFF) para apertura perfecta directa en Excel / Hojas de Cálculo
                using (var sw = new StreamWriter(rutaArchivo, false, new UTF8Encoding(true)))
                {
                    sw.Write(sb.ToString());
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Error al exportar a Hoja de Cálculo: {ex.Message}";
                return false;
            }
        }
    }
}
