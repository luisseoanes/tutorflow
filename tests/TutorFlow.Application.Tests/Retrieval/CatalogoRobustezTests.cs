using TutorFlow.Application.Retrieval;
using TutorFlow.Infrastructure.Retrieval;

namespace TutorFlow.Application.Tests.Retrieval;

public class CatalogoRobustezTests
{
    [Fact]
    public async Task Directorio_inexistente_da_catalogo_vacio_con_advertencia_y_sin_excepcion()
    {
        var avisos = new List<string>();
        var ruta = Path.Combine(Path.GetTempPath(), "tf-no-existe-" + Guid.NewGuid().ToString("N"));

        var catalogo = new KnowledgeCatalog(ruta, [CorpusHelper.DefaultQuarantine], avisos.Add);

        Assert.Empty(catalogo.Chunks);
        Assert.Single(avisos);
        Assert.Contains(ruta, avisos[0], StringComparison.Ordinal);
        Assert.Empty(await new Bm25Retriever(catalogo).RecuperarAsync("nota mínima", CancellationToken.None));
        Assert.Empty(await new FullContextRetriever(catalogo).RecuperarAsync("nota mínima", CancellationToken.None));
    }

    [Fact]
    public void Directorio_vacio_da_catalogo_vacio_con_advertencia()
    {
        var avisos = new List<string>();
        var dir = Directory.CreateTempSubdirectory("tf-vacio-").FullName;
        try
        {
            var catalogo = new KnowledgeCatalog(dir, [CorpusHelper.DefaultQuarantine], avisos.Add);

            Assert.Empty(catalogo.Chunks);
            Assert.Single(avisos);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
