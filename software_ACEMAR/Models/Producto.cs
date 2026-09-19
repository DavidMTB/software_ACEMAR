namespace software_ACEMAR.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal CuMax { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";

        // Propiedad calculada para determinar si requiere alerta visual
        public bool EsStockBajo => StockActual <= StockMinimo;
    }
}
