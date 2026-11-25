using System.Text.RegularExpressions;
using TutorFlow.Application.Retrieval;

namespace TutorFlow.Application.Tests.Retrieval;

public class ChunkingTests
{
    [Fact]
    [Trait("Criterio", "RAG-15.a")]
    public void Cada_articulo_de_los_reglamentos_produce_un_chunk_y_el_2023_no_es_vigente()
    {
        var chunks = CorpusHelper.BuildDefault();
        foreach (var doc in CorpusHelper.Documents().Where(d => d.Nombre.StartsWith("reglamento_academico_", StringComparison.Ordinal)))
        {
            var numeros = Regex.Matches(doc.Contenido, @"^\*\*Artículo (\d+)\.", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(numeros);
            var delDoc = chunks.Where(c => c.Documento == doc.Nombre).ToList();
            Assert.Equal(numeros.Count, delDoc.Count);
            foreach (var n in numeros)
            {
                var chunk = Assert.Single(delDoc, c => c.Seccion == $"Artículo {n}");
                Assert.Equal($"{doc.Nombre}#Artículo {n}", chunk.ChunkId);
                Assert.Equal(!doc.Nombre.Contains("2023", StringComparison.Ordinal), chunk.Vigente);
            }
        }

        Assert.All(chunks.Where(c => c.Documento == "reglamento_academico_2023.md"), c => Assert.False(c.Vigente));
        Assert.All(chunks.Where(c => c.Documento != "reglamento_academico_2023.md"), c => Assert.True(c.Vigente));
    }

    [Fact]
    [Trait("Criterio", "RAG-15.b")]
    public void Tabla_de_cortes_queda_completa_en_un_solo_chunk()
    {
        var chunks = CorpusHelper.BuildDefault();
        var conTabla = chunks.Where(c => c.Documento == "evaluacion_y_calificaciones.md" && c.Texto.Contains("| Corte 1 |", StringComparison.Ordinal)).ToList();
        var chunk = Assert.Single(conTabla);
        Assert.Equal("Sección 2", chunk.Seccion);
        foreach (var fila in new[] { "| Corte |", "| --- |", "| Corte 1 | 1 a 5 | 30% |", "| Corte 2 | 6 a 10 | 30% |", "| Corte 3 | 11 a 16 | 40% |" })
        {
            Assert.Contains(fila, chunk.Texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Criterio", "RAG-15.c")]
    public void Secciones_padre_sin_texto_propio_no_generan_chunk()
    {
        var chunks = CorpusHelper.BuildDefault();
        Assert.DoesNotContain(chunks, c => c.ChunkId == "bienestar_y_becas.md#Sección 3");
        Assert.Contains(chunks, c => c.ChunkId == "bienestar_y_becas.md#Sección 3.1");
        Assert.Contains(chunks, c => c.ChunkId == "bienestar_y_becas.md#Sección 3.2");
        // Los capitulos de los reglamentos tampoco generan chunk.
        Assert.DoesNotContain(chunks, c => c.Seccion.StartsWith("Capítulo", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Criterio", "RAG-15.d")]
    public void Dos_indexaciones_producen_los_mismos_chunkId_y_hash()
    {
        var a = CorpusHelper.BuildDefault();
        var b = CorpusHelper.BuildDefault();
        Assert.Equal(a.Select(c => (c.ChunkId, c.Hash)), b.Select(c => (c.ChunkId, c.Hash)));
        Assert.Equal(a.Count, a.Select(c => c.ChunkId).Distinct().Count());
        Assert.All(a, c => Assert.False(string.IsNullOrWhiteSpace(c.Hash)));
    }
}
