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

        public decimal PrecioUnitario { get; set; }

        public decimal StockActual { get; set; }

        public decimal StockMinimo { get; set; }

        public decimal CuMax { get; set; }

        [MaxLength(50)]
        public string UnidadMedida { get; set; } = "Unidad";

        [MaxLength(500)]
        public string? UnidadesPermitidas { get; set; }

        [MaxLength(1000)]
        public string? PreciosPorUnidad { get; set; }

        [MaxLength(1000)]
        public string? FactoresPorUnidad { get; set; }

        public List<LoteProducto> Lotes { get; set; } = new();

        public List<ProductoProveedor> ProductosProveedor { get; set; } = new();

        public bool EsStockBajo => StockActual <= StockMinimo;

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

        public void SetUnidadesPermitidas(List<string> unidades)
        {
            if (unidades == null || unidades.Count == 0)
            {
                UnidadesPermitidas = null;
                return;
            }
            UnidadesPermitidas = System.Text.Json.JsonSerializer.Serialize(unidades);
        }

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

        public void GuardarPreciosPorUnidad(Dictionary<string, decimal> precios)
        {
            if (precios == null || precios.Count == 0)
            {
                PreciosPorUnidad = null;
                return;
            }
            PreciosPorUnidad = System.Text.Json.JsonSerializer.Serialize(precios);
        }

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

        public void GuardarFactoresPorUnidad(Dictionary<string, decimal> factores)
        {
            if (factores == null || factores.Count == 0)
            {
                FactoresPorUnidad = null;
                return;
            }
            FactoresPorUnidad = System.Text.Json.JsonSerializer.Serialize(factores);
        }

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

            if (string.Equals(UnidadMedida, "Metros", StringComparison.OrdinalIgnoreCase) && string.Equals(uNorm, "Bolsa", StringComparison.OrdinalIgnoreCase)) return 0.04m;
            if (string.Equals(uNorm, "Volquetada", StringComparison.OrdinalIgnoreCase)) return 4.0m;
            if (string.Equals(uNorm, "Ciento", StringComparison.OrdinalIgnoreCase)) return 100.0m;
            if (string.Equals(uNorm, "Millar", StringComparison.OrdinalIgnoreCase)) return 1000.0m;
            if (string.Equals(uNorm, "Par", StringComparison.OrdinalIgnoreCase)) return 2.0m;
            if (string.Equals(uNorm, "Carretilla", StringComparison.OrdinalIgnoreCase)) return 0.08m;
            if (string.Equals(uNorm, "Lata", StringComparison.OrdinalIgnoreCase)) return 0.02m;

            return 1.0m;
        }

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
