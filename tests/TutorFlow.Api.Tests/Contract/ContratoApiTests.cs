using System.Net;
using System.Text.Json;
using TutorFlow.Api.Tests.Infra;

namespace TutorFlow.Api.Tests.Contract;

/// <summary>
/// RES-02: forma exacta del contrato. Cada respuesta HTTP real del host en proceso (LLM guionizado, sin red ni
/// llave) se valida contra el JSON Schema de <see cref="EsquemaContrato"/>.
/// </summary>
public class ContratoApiTests
{
    private const string Nota = "¿Cuál es la nota de Bases de Datos de Andrés Martínez?";

    public static TheoryData<int> Ids => new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    /// <summary>Guion plausible por pregunta de ejemplo (id 1..10) de q10/material/preguntas_ejemplo.json.</summary>
    private static LlmGuion Guion(int id) => id switch
    {
        1 => new(LlmGuion.Final("La nota mínima para aprobar es 3.0, en escala de 0.0 a 5.0.")),
        2 => new(LlmGuion.Invoca("obtener_resumen_academico"), LlmGuion.Final("Tu promedio ponderado es 4.39.")),
        3 => new(LlmGuion.Invoca("obtener_asistencia", "{\"asignatura\":\"Bases de Datos\"}"), LlmGuion.Final("Tienes 2 faltas de 48 clases.")),
        4 => new(LlmGuion.Final("30% corte 1, 30% corte 2 y 40% corte 3, redondeada a una cifra decimal.")),
        5 => new(LlmGuion.Invoca("obtener_notas"), LlmGuion.Final("Tu definitiva en Contabilidad es 3.9.")),
        6 => new(LlmGuion.Invoca("asignaturas_perdidas_programa"), LlmGuion.Final("Valentina Castro: Microeconomía 1.8 y Estadística 2.3.")),
        7 => new(LlmGuion.Final("No encuentro esa información en los documentos disponibles.")),
        8 => new(LlmGuion.Invoca("evaluar_beca_rendimiento"), LlmGuion.Final("Revisa los requisitos de la beca en el reglamento.")),
        9 => new(LlmGuion.Final("No encuentro esa información en los documentos disponibles.")),
        _ => new(LlmGuion.Final("Esta respuesta no deberia llegar a darse.")),
    };

    private static JsonElement Pregunta(int id)
    {
        var ruta = Path.Combine(RutaRepo.Raiz, "q10", "material", "preguntas_ejemplo.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
        return doc.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == id).Clone();
    }

    private static async Task<(HttpResponseMessage Respuesta, string Cuerpo)> EnviarAsync(FabricaE2E f, string rol, string id, string pregunta)
    {
        var r = await FabricaE2E.PreguntarAsync(f.CreateClient(), rol, id, pregunta);
        return (r, await r.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(Ids))]
    [Trait("Criterio", "RES-02.a")]
    public async Task RES_02_las_diez_preguntas_de_ejemplo_producen_200_o_403_que_validan_contra_el_esquema(int id)
    {
        var p = Pregunta(id);
        var usuario = p.GetProperty("usuario");
        using var f = new FabricaE2E { Llm = Guion(id) };

        var (r, cuerpo) = await EnviarAsync(
            f, usuario.GetProperty("rol").GetString()!, usuario.GetProperty("id").GetString()!, p.GetProperty("pregunta").GetString()!);

        Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden, $"pregunta {id}: {(int)r.StatusCode}");
        Assert.Equal(id == 10 ? HttpStatusCode.Forbidden : HttpStatusCode.OK, r.StatusCode);
        Assert.Empty(EsquemaContrato.Validar(cuerpo));
        using var doc = JsonDocument.Parse(cuerpo);
        Assert.True(doc.RootElement.GetProperty("tokens").GetInt64() >= 0);
        Assert.True(doc.RootElement.GetProperty("costoUsd").GetDecimal() >= 0m);
    }

    [Fact]
    [Trait("Criterio", "RES-02.b")]
    public async Task RES_02_los_cuerpos_403_acceso_y_403_identidad_validan_contra_el_mismo_esquema()
    {
        using var f = new FabricaE2E { Llm = Guion(10) };

        var (acceso, cuerpoAcceso) = await EnviarAsync(f, "estudiante", "EST-001", Nota);
        var (identidad, cuerpoIdentidad) = await EnviarAsync(f, "coordinador", "EST-001", "¿Cuál es mi promedio?");

        Assert.Equal(HttpStatusCode.Forbidden, acceso.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, identidad.StatusCode);
        Assert.Empty(EsquemaContrato.Validar(cuerpoAcceso));
        Assert.Empty(EsquemaContrato.Validar(cuerpoIdentidad));
        // Son cuerpos distintos: el de acceso cita el Art. 22; el de identidad no cita nada.
        using var a = JsonDocument.Parse(cuerpoAcceso);
        using var i = JsonDocument.Parse(cuerpoIdentidad);
        Assert.Equal(1, a.RootElement.GetProperty("citas").GetArrayLength());
        Assert.Equal(0, i.RootElement.GetProperty("citas").GetArrayLength());
    }

    [Fact]
    [Trait("Criterio", "RES-02.c")]
    public async Task RES_02_el_content_type_de_las_respuestas_200_y_403_es_application_json()
    {
        using var f = new FabricaE2E { Llm = Guion(1) };

        var (ok, _) = await EnviarAsync(f, "estudiante", "EST-001", "¿Cuál es la nota mínima para aprobar una asignatura?");
        var (acceso, _) = await EnviarAsync(f, "estudiante", "EST-001", Nota);
        var (identidad, _) = await EnviarAsync(f, "coordinador", "EST-001", "¿Cuál es mi promedio?");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, acceso.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, identidad.StatusCode);
        Assert.All(new[] { ok, acceso, identidad }, r => Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType));
    }

    // ----------------------------------------------------- el validador no es una compuerta decorativa

    [Fact]
    [Trait("Criterio", "RES-02.a")]
    public void RES_02_el_validador_acepta_un_cuerpo_correcto_y_rechaza_los_que_rompen_el_contrato()
    {
        const string bien = "{\"respuesta\":\"x\",\"citas\":[{\"documento\":\"d\",\"seccion\":\"s\"}],\"usoDatosEstudiante\":false,\"tokens\":0,\"costoUsd\":0.000123}";
        Assert.Empty(EsquemaContrato.Validar(bien));

        string[] rotos =
        [
            bien.Replace("\"tokens\":0", "\"tokens\":0,\"extra\":1", StringComparison.Ordinal),
            bien.Replace("\"seccion\":\"s\"", "\"seccion\":\"s\",\"pagina\":3", StringComparison.Ordinal),
            bien.Replace("\"tokens\":0", "\"tokens\":1.5", StringComparison.Ordinal),
            bien.Replace("\"tokens\":0", "\"tokens\":-1", StringComparison.Ordinal),
            bien.Replace("\"costoUsd\":0.000123", "\"costoUsd\":-0.1", StringComparison.Ordinal),
            bien.Replace("\"citas\":[{\"documento\":\"d\",\"seccion\":\"s\"}]", "\"citas\":null", StringComparison.Ordinal),
            bien.Replace("\"respuesta\":\"x\",", string.Empty, StringComparison.Ordinal),
            bien.Replace("\"usoDatosEstudiante\"", "\"UsoDatosEstudiante\"", StringComparison.Ordinal),
            bien.Replace("\"documento\":\"d\",", string.Empty, StringComparison.Ordinal),
        ];
        Assert.All(rotos, roto => Assert.NotEmpty(EsquemaContrato.Validar(roto)));
    }
}
