using TutorFlow.Application.Retrieval;

namespace TutorFlow.Application.Tests.Retrieval;

public class Bm25Tests
{
    private static async Task<IReadOnlyList<string>> Top(string pregunta)
    {
        var r = await new Bm25Retriever(CorpusHelper.DefaultCatalog()).RecuperarAsync(pregunta, CancellationToken.None);
        return r.Select(f => f.ChunkId).ToList();
    }

    [Fact]
    public async Task Nota_minima_encuentra_articulo_5_del_2025_primero()
    {
        var top = await Top("¿Cuál es la nota mínima para aprobar una asignatura?");
        Assert.Equal("reglamento_academico_2025.md#Artículo 5", top[0]);
    }

    [Fact]
    public async Task Horario_de_bienestar_encuentra_seccion_1()
    {
        var top = await Top("¿A qué hora atiende la oficina de Bienestar?");
        Assert.Equal("bienestar_y_becas.md#Sección 1", top[0]);
    }

    [Fact]
    public async Task Sin_mencion_2023_no_se_devuelven_chunks_no_vigentes()
    {
        var top = await Top("¿Cuál es el plazo para cancelar una asignatura?");
        Assert.Equal("reglamento_academico_2025.md#Artículo 15", top[0]);
        Assert.DoesNotContain(top, id => id.StartsWith("reglamento_academico_2023.md", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("¿Qué decía el reglamento 2023 sobre la habilitación?")]
    [InlineData("¿Y según el reglamento anterior, hay habilitación?")]
    public async Task Con_mencion_explicita_aparece_el_2023(string pregunta)
    {
        var top = await Top(pregunta);
        Assert.Contains("reglamento_academico_2023.md#Artículo 6", top);
        Assert.True(top.Count <= 4);
    }

    [Fact]
    public async Task Top_k_es_4_como_maximo_y_beca_apoyo_aparece()
    {
        var top = await Top("¿Cómo se solicita la beca de apoyo socioeconómico?");
        Assert.True(top.Count <= 4);
        Assert.Contains("bienestar_y_becas.md#Sección 3.2", top);
    }

    [Fact]
    public async Task Pregunta_sin_terminos_utiles_no_falla()
    {
        var r = await new Bm25Retriever(CorpusHelper.DefaultCatalog()).RecuperarAsync("", CancellationToken.None);
        Assert.NotNull(r);
    }

    [Fact]
    public void Normalizador_quita_tildes_stopwords_y_plurales()
    {
        var t = SpanishTextNormalizer.Tokenize("Las Calificaciones de los Estudiantes, ÁRBOL");
        Assert.DoesNotContain("las", t);
        Assert.DoesNotContain("de", t);
        Assert.Equal(SpanishTextNormalizer.Tokenize("calificacion estudiante"), SpanishTextNormalizer.Tokenize("calificaciones estudiantes"));
        Assert.Equal(SpanishTextNormalizer.Tokenize("arbol"), SpanishTextNormalizer.Tokenize("ÁRBOL"));
    }
}
