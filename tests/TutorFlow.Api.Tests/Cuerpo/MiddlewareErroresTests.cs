using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TutorFlow.Api.Tests.Infra;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Preguntar;
using TutorFlow.Domain.Academico;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Api.Tests.Infra.Asercion;

namespace TutorFlow.Api.Tests.Cuerpo;

/// <summary>Mapeo central de excepciones tipadas (SPEC 3.3 y 3.4) con un caso de uso que lanza cada una.</summary>
public class MiddlewareErroresTests
{
    private sealed class CasoUsoQueLanza(Exception ex) : IPreguntarAsistente
    {
        public Task<PreguntarResultado> EjecutarAsync(PreguntarComando comando, CancellationToken ct) => throw ex;
    }

    private static FabricaApi Con(Exception ex) => new(s =>
    {
        s.RemoveAll<IPreguntarAsistente>();
        s.AddSingleton<IPreguntarAsistente>(new CasoUsoQueLanza(ex));
    });

    private const string Peticion = "{\"pregunta\":\"hola\",\"usuario\":{\"rol\":\"estudiante\",\"id\":\"EST-001\"}}";

    [Fact]
    [Trait("Contrato", "SPEC-3.4")]
    public async Task AccesoDenegado_estudiante_responde_403_acceso_con_el_cuerpo_exacto()
    {
        using var f = Con(new AccesoDenegadoException(RolAcademico.Estudiante, "referencia"));

        var r = await PostAsync(f.CreateClient(), Peticion);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal(
            "{\"respuesta\":\"No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.\","
            + "\"citas\":[{\"documento\":\"reglamento_academico_2025.md\",\"seccion\":\"Artículo 22\"}],"
            + "\"usoDatosEstudiante\":false,\"tokens\":0,\"costoUsd\":0}",
            await r.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Contrato", "SPEC-3.4")]
    public async Task AccesoDenegado_coordinador_usa_su_mensaje_y_refleja_el_consumo_indicado()
    {
        using var f = Con(new AccesoDenegadoException(RolAcademico.Coordinador, "salida", 120, 0.000512m));

        var r = await PostAsync(f.CreateClient(), Peticion);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal(
            "No tienes permiso para consultar esa información. Solo puedes consultar los datos de estudiantes de los programas que coordinas.",
            doc.RootElement.GetProperty("respuesta").GetString());
        Assert.Equal("Artículo 22", doc.RootElement.GetProperty("citas")[0].GetProperty("seccion").GetString());
        Assert.False(doc.RootElement.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(120, doc.RootElement.GetProperty("tokens").GetInt32());
        Assert.Equal(0.000512m, doc.RootElement.GetProperty("costoUsd").GetDecimal());
    }

    [Fact]
    [Trait("Contrato", "SPEC-3.4")]
    public async Task IdentidadInvalida_responde_el_cuerpo_exacto_del_403_identidad_sin_citar_el_Art_22()
    {
        using var f = Con(new IdentidadInvalidaException("motivo interno"));

        var r = await PostAsync(f.CreateClient(), Peticion);

        Assert.Equal(
            "{\"respuesta\":\"No fue posible validar la identidad indicada en la solicitud.\",\"citas\":[],"
            + "\"usoDatosEstudiante\":false,\"tokens\":0,\"costoUsd\":0}",
            await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Dependencias503))]
    [Trait("Contrato", "SPEC-3.3")]
    public async Task Dependencia_o_dato_invalido_responden_503_problem_json(Exception ex)
    {
        using var f = Con(ex);

        var r = await PostAsync(f.CreateClient(), Peticion);

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
    }

    public static TheoryData<Exception> Dependencias503() => new()
    {
        new DependenciaNoDisponibleException("x"),
        new DatoAcademicoInvalidoException("x"),
        new LlmNoDisponibleException("x", 503),
    };

    [Fact]
    [Trait("Contrato", "SPEC-3.3")]
    public async Task LlmNoDisponible_responde_503_problem_json_sin_el_mensaje_ni_el_cuerpo_del_proveedor()
    {
        using var f = Con(new LlmNoDisponibleException("llave test-key-NOT-REAL; cuerpo {\"error\":\"secreto-del-proveedor\"}", 401));

        var r = await PostAsync(f.CreateClient(), Peticion);

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-key-NOT-REAL", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto-del-proveedor", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("401", JsonDocument.Parse(cuerpo).RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }
}
