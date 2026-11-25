using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Retrieval;
using TutorFlow.Application.Security;
using TutorFlow.Application.Tests.Retrieval;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Llm.Dobles;

namespace TutorFlow.Application.Tests.Llm;

/// <summary>
/// T-06.3: fijan las reglas GENERALES de redaccion y de uso de herramientas del prompt de sistema (sin acoplarse a
/// preguntas del dataset) y caracterizan la causa de un 403 "salida" observado con un LLM real.
/// </summary>
public class InstruccionesSistemaTests
{
    private static async Task<string> SistemaEnviado(PrincipalAcademico principal)
    {
        var catalogo = CorpusHelper.DefaultCatalog();
        var llm = new LlmGuionizado(Texto(Salida("ok", "reglamento_academico_2025.md#Artículo 5")));
        var fragmentos = await new FullContextRetriever(catalogo).RecuperarAsync("pregunta cualquiera", CancellationToken.None);
        await new GeneradorRespuesta(llm, new GeneradorOptions(), catalogo)
            .GenerarAsync(principal, "pregunta cualquiera", fragmentos, new DespachadorGuionizado(), CancellationToken.None);
        return llm.Solicitudes[0].Sistema;
    }

    [Theory]
    [InlineData("DEBES llamar primero a la herramienta")]
    [InlineData("nunca un nombre de persona")]
    [InlineData("omite estudianteId")]
    [InlineData("denominador")]
    [InlineData("porcentajes, pesos")]
    [InlineData("todos los requisitos")]
    [InlineData("\"derogado\"")]
    [InlineData("remite al coordinador del programa")]
    [InlineData("no escribas codigos ni ids internos")]
    public async Task El_sistema_contiene_las_reglas_generales_de_redaccion_y_herramientas(string fragmento)
    {
        Assert.Contains(fragmento, await SistemaEnviado(Estudiante()), StringComparison.Ordinal);
        Assert.Contains(fragmento, await SistemaEnviado(Coordinador()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_sistema_no_incluye_nombres_ni_ids_del_dataset()
    {
        var s = await SistemaEnviado(Estudiante());
        Assert.DoesNotContain("Laura", s, StringComparison.Ordinal);
        Assert.DoesNotContain("EST-0", s, StringComparison.Ordinal);
        Assert.DoesNotContain("COORD-", s, StringComparison.Ordinal);
    }

    /// <summary>
    /// Causa del 403 "salida" de q06 con bm25: un codigo de asignatura con forma de id de estudiante escrito en la
    /// respuesta lo marca L5 como id ausente del roster. La regla 11 del prompt evita que el modelo lo escriba.
    /// </summary>
    [Fact]
    public void Un_codigo_de_asignatura_con_forma_de_id_en_la_respuesta_lo_marca_L5()
    {
        var roster = new RosterSnapshot(
            [new RosterEstudiante("EST-001", "laura gomez", "ADM")],
            [new RosterCoordinador("COORD-02")],
            [new RosterPrograma("ADM", "administracion")],
            DateTimeOffset.UnixEpoch);
        var coordinador = new PrincipalAcademico(RolAcademico.Coordinador, "COORD-02", ["ADM"]);

        Assert.True(GuardiaSalida.Evaluar("Perdio Estadistica (EST-103).", coordinador, roster).Marcada);
        Assert.False(GuardiaSalida.Evaluar("Perdio Estadistica.", coordinador, roster).Marcada);
    }

    // T-06.4 (INT-023): cifras de herramientas y normativas, preguntas normativas y citas.

    [Theory]
    [Trait("Criterio", "ACAD-07.c")]
    [Trait("Criterio", "AUTH-02.b")]
    [Trait("Criterio", "RAG-07.b")]
    [Trait("Criterio", "RAG-10.c")]
    [Trait("Criterio", "ACAD-15.c")]
    [Trait("Criterio", "RAG-01.c")]
    [InlineData("ACAD-07.c", "tal como los devuelve la herramienta")]
    [InlineData("ACAD-07.c", "sin reformatearlos")]
    [InlineData("AUTH-02.b", "No omitas el denominador")]
    [InlineData("RAG-07.b", "escribelos tal como aparecen en el fragmento")]
    [InlineData("RAG-07.b", "no \"0.3\"")]
    [InlineData("RAG-10.c", "NO invoques herramientas de datos academicos")]
    [InlineData("RAG-10.c", "no mezcles datos del usuario")]
    [InlineData("ACAD-15.c", "pide datos propios: consulta la herramienta")]
    [InlineData("RAG-01.c", "solo los fragmentos de los que tomaste un dato")]
    public async Task El_sistema_refuerza_cifras_preguntas_normativas_y_citas(string criterio, string fragmento)
    {
        Assert.NotEmpty(criterio);
        Assert.Contains(fragmento, await SistemaEnviado(Estudiante()), StringComparison.Ordinal);
        Assert.Contains(fragmento, await SistemaEnviado(Coordinador()), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Criterio", "ACAD-07.c")]
    public async Task El_resultado_de_herramienta_se_presenta_como_cifras_a_citar_textualmente_sin_alterar_el_contenido()
    {
        const string contenido = """{"asignatura":"Bases de Datos","faltas":9,"clases":48,"porcentaje":"18.75%"}""";
        var catalogo = CorpusHelper.DefaultCatalog();
        var llm = new LlmGuionizado(Invoca("obtener_asistencia"), Texto(Salida("ok", "reglamento_academico_2025.md#Artículo 5")));
        var despachador = new DespachadorGuionizado(_ => new ResultadoHerramienta(contenido, [new CitaDocumental("Datos del estudiante", "Asistencia")], true, "X-1"));
        var fragmentos = await new FullContextRetriever(catalogo).RecuperarAsync("faltas", CancellationToken.None);

        await new GeneradorRespuesta(llm, new GeneradorOptions(), catalogo)
            .GenerarAsync(Estudiante(), "faltas", fragmentos, despachador, CancellationToken.None);

        var mensajes = llm.Solicitudes[1].Mensajes;
        var iHerr = mensajes.ToList().FindIndex(m => m.Rol == RolMensaje.Herramienta);
        Assert.Equal(contenido, mensajes[iHerr].Contenido);
        Assert.Equal(RolMensaje.Usuario, mensajes[iHerr + 1].Rol);
        Assert.Contains("citar textualmente", mensajes[iHerr + 1].Contenido, StringComparison.Ordinal);
        Assert.Contains("sin recalcular ni reformatear", mensajes[iHerr + 1].Contenido, StringComparison.Ordinal);
    }
}
