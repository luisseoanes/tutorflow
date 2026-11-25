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
}
