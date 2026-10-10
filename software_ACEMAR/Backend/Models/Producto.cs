using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace software_ACEMAR.Models
{
    public class Producto
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Categoria { get; set; } = string.Empty;

        /// <summary>
        /// Precio de referencia del producto. Cuando el producto tiene lotes,
        /// el precio real de cada venta viene del LoteProducto.PrecioVenta.
        /// Este campo sirve como precio sugerido para nuevos lotes.
        /// </summary>
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// Stock total calculado como suma de lotes activos.
        /// Se mantiene sincronizado por el LoteService al crear/consumir lotes.
        /// </summary>
        public decimal StockActual { get; set; }

        public decimal StockMinimo { get; set; }

        /// <summary>Capacidad máxima de almacenamiento (CuMax). Para alertas de sobrecapacidad.</summary>
        public decimal CuMax { get; set; }

        /// <summary>Unidad de medida principal/por defecto del producto.</summary>
        [MaxLength(50)]
        public string UnidadMedida { get; set; } = "Unidad";

        /// <summary>
        /// Lista de unidades de medida permitidas para este producto, almacenada como
        /// JSON serializado: '["Unidad","Saco","Bolsa"]'.
        /// Null o vacío = usar solo UnidadMedida como única opción.
        /// </summary>
        [MaxLength(500)]
        public string? UnidadesPermitidas { get; set; }

        /// <summary>
        /// Mapeo JSON de precios por unidad de medida: '{"Unidad":10.5,"Bolsa":100.0,"Caja":250.0}'.
        /// </summary>
        [MaxLength(1000)]
        public string? PreciosPorUnidad { get; set; }

        /// <summary>
        /// Mapeo JSON de factores de conversión/equivalencia respecto a la unidad base: '{"Volquetada":4.0,"Carretilla":0.25,"Lata":0.1}'.
        /// Indica cuántas unidades de la U.M. principal representa cada unidad adicional.
        /// </summary>
        [MaxLength(1000)]
        public string? FactoresPorUnidad { get; set; }

        // Relación con lotes
        public List<LoteProducto> Lotes { get; set; } = new();

        // Relación con proveedores
        public List<ProductoProveedor> ProductosProveedor { get; set; } = new();

        // Propiedad calculada para determinar si requiere alerta visual
        public bool EsStockBajo => StockActual <= StockMinimo;

        /// <summary>
        /// Obtiene la lista de unidades permitidas parseada, o la unidad por defecto si no hay lista.
        /// </summary>
        public List<string> ObtenerUnidadesDisponibles()
        {
            if (!string.IsNullOrWhiteSpace(UnidadesPermitidas))
            {
                try
                {
                    var lista = System.Text.Json.JsonSerializer.Deserialize<List<string>>(UnidadesPermitidas);
                    if (lista != null && lista.Count > 0)
                        return lista;
                }
                catch
                {
                    // Si viene como lista separada por comas (ej: "Lata, Bolsa, Metros")
                    var partes = UnidadesPermitidas.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                    var res = new List<string>();
                    foreach (var p in partes)
                    {
                        string trim = p.Trim(' ', '"', '[', ']');
                        if (!string.IsNullOrWhiteSpace(trim)) res.Add(trim);
                    }
                    if (res.Count > 0) return res;
                }
            }

            return new List<string> { UnidadMedida };
        }

        public List<string> ObtenerUnidadesPermitidas() => ObtenerUnidadesDisponibles();

        /// <summary>
        /// Serializa una lista de unidades al campo UnidadesPermitidas.
        /// </summary>
        public void SetUnidadesPermitidas(List<string> unidades)
        {
            if (unidades == null || unidades.Count == 0)
            {
                UnidadesPermitidas = null;
                return;
            }
            UnidadesPermitidas = System.Text.Json.JsonSerializer.Serialize(unidades);
        }

        /// <summary>
        /// Obtiene el diccionario de precios asignados a cada unidad de medida.
        /// </summary>
        public Dictionary<string, decimal> ObtenerPreciosPorUnidad()
        {
            var dict = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(PreciosPorUnidad))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(PreciosPorUnidad);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                            dict[kvp.Key] = kvp.Value;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(UnidadMedida) && !dict.ContainsKey(UnidadMedida))
            {
                dict[UnidadMedida] = PrecioUnitario;
            }

            return dict;
        }

        /// <summary>
        /// Serializa un diccionario de precios por unidad al campo PreciosPorUnidad.
        /// </summary>
        public void GuardarPreciosPorUnidad(Dictionary<string, decimal> precios)
        {
            if (precios == null || precios.Count == 0)
            {
                PreciosPorUnidad = null;
                return;
            }
            PreciosPorUnidad = System.Text.Json.JsonSerializer.Serialize(precios);
        }

        /// <summary>
        /// Obtiene el diccionario de factores de conversión asignados a cada unidad de medida.
        /// </summary>
        public Dictionary<string, decimal> ObtenerFactoresPorUnidad()
        {
            var dict = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(FactoresPorUnidad))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(FactoresPorUnidad);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                            dict[kvp.Key] = kvp.Value;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(UnidadMedida) && !dict.ContainsKey(UnidadMedida))
            {
                dict[UnidadMedida] = 1.0m;
            }

            return dict;
        }

        /// <summary>
        /// Serializa un diccionario de factores por unidad al campo FactoresPorUnidad.
        /// </summary>
        public void GuardarFactoresPorUnidad(Dictionary<string, decimal> factores)
        {
            if (factores == null || factores.Count == 0)
            {
                FactoresPorUnidad = null;
                return;
            }
            FactoresPorUnidad = System.Text.Json.JsonSerializer.Serialize(factores);
        }

        /// <summary>
        /// Obtiene el factor multiplicador de conversión respecto a la unidad base.
        /// Ejemplo: Volquetada = 4.0 (1 Volquetada = 4 Metros base), Carretilla = 0.25 (0.25 Metros base).
        /// </summary>
        public decimal ObtenerFactorParaUnidad(string? unidad)
        {
            if (string.IsNullOrWhiteSpace(unidad)) return 1.0m;
            string uNorm = unidad.Trim();
            if (string.Equals(uNorm, UnidadMedida?.Trim(), StringComparison.OrdinalIgnoreCase)) return 1.0m;

            var dict = ObtenerFactoresPorUnidad();
            if (dict.TryGetValue(uNorm, out decimal factor) && factor > 0)
            {
                return factor;
            }

            // Factores estándar predeterminados por nombre si no hay factor personalizado guardado
            if (string.Equals(uNorm, "Volquetada", StringComparison.OrdinalIgnoreCase)) return 4.0m;
            if (string.Equals(uNorm, "Ciento", StringComparison.OrdinalIgnoreCase)) return 100.0m;
            if (string.Equals(uNorm, "Millar", StringComparison.OrdinalIgnoreCase)) return 1000.0m;
            if (string.Equals(uNorm, "Par", StringComparison.OrdinalIgnoreCase)) return 2.0m;
            if (string.Equals(uNorm, "Carretilla", StringComparison.OrdinalIgnoreCase)) return 0.25m;
            if (string.Equals(uNorm, "Lata", StringComparison.OrdinalIgnoreCase)) return 0.1m;

            return 1.0m;
        }

        /// <summary>
        /// Obtiene el precio específico para una unidad de medida seleccionada o el precio unitario base.
        /// </summary>
        public decimal ObtenerPrecioParaUnidad(string unidad)
        {
            if (string.IsNullOrWhiteSpace(unidad)) return PrecioUnitario;
            var dict = ObtenerPreciosPorUnidad();
            if (dict.TryGetValue(unidad.Trim(), out decimal precio) && precio > 0)
            {
                return precio;
            }
            return PrecioUnitario;
        }
    }
}
