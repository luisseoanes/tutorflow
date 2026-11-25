using TutorFlow.Application.Retrieval;

namespace TutorFlow.Application.Tests.Retrieval;

public class DocumentoAdicionalTests
{
    [Fact]
    [Trait("Criterio", "RES-11.b")]
    public async Task Un_documento_adicional_valido_se_indexa_sin_cambiar_el_codigo()
    {
        const string contenido = "# Servicios de apoyo\n\n## 1. Tutorías de escritura\n\nLas tutorías de escritura se ofrecen los lunes de 10:00 a 12:00.\n\n## 2. Préstamo de equipos\n\nSe prestan portátiles por 24 horas.\n";
        var documentos = CorpusHelper.Documents().Append(new KnowledgeDocument("servicios_apoyo.md", contenido)).ToList();

        var chunks = KnowledgeIndexer.Build(documentos, [CorpusHelper.DefaultQuarantine]);

        var nuevo = chunks.Where(c => c.Documento == "servicios_apoyo.md").ToList();
        Assert.Equal(2, nuevo.Count);
        Assert.Contains(nuevo, c => c.ChunkId == "servicios_apoyo.md#Sección 1" && c.Texto.Contains("lunes de 10:00", StringComparison.Ordinal));
        Assert.All(nuevo, c => Assert.True(c.Vigente && !c.Untrusted));
        // Los demas chunks no cambian por incorporar el documento nuevo.
        Assert.Equal(CorpusHelper.BuildDefault().Count + 2, chunks.Count);

        var catalogo = new Catalogo(chunks);
        var encontrados = await new Bm25Retriever(catalogo).RecuperarAsync("tutorías de escritura lunes", CancellationToken.None);
        Assert.Equal("servicios_apoyo.md#Sección 1", encontrados[0].ChunkId);
    }

    private sealed class Catalogo(IReadOnlyList<Chunk> chunks) : IChunkCatalog
    {
        public IReadOnlyList<Chunk> Chunks { get; } = chunks;
    }
}
