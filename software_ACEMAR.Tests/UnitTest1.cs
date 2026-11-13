using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using software_ACEMAR.Data;

namespace software_ACEMAR.Tests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void Verificar_ArenaGruesa_UnidadBaseEsMetros()
    {
        using var context = new AppDbContext();
        context.InitializeDatabase();

        var arena = context.Productos.FirstOrDefault(p => p.Codigo == "ARE-003");
        Assert.IsNotNull(arena);
        Assert.AreEqual("Metros", arena.UnidadMedida);
        Assert.AreEqual(1.0m, arena.ObtenerFactorParaUnidad("Metros"));
        Assert.AreEqual(0.04m, arena.ObtenerFactorParaUnidad("Bolsa"));
        Assert.AreEqual(4.0m, arena.ObtenerFactorParaUnidad("Volquetada"));
    }
}
