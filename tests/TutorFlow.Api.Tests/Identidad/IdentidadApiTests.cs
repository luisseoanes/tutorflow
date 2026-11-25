using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Api.Tests.Infra;
using static TutorFlow.Api.Tests.Infra.Asercion;

namespace TutorFlow.Api.Tests.Identidad;

/// <summary>L1 vista por HTTP (SPEC 3.3 regla 5, 3.4). L1 es consistencia, no autenticacion.</summary>
public class IdentidadApiTests
{
    [Fact]
    [Trait("Criterio", "AUTH-08.a")]
    public async Task Coordinador_con_id_de_estudiante_responde_403_identidad()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(
            Usuario("coordinador", "EST-001"),
            "¿Qué estudiantes de mi programa tienen materias perdidas?"));

        await Afirmar403IdentidadAsync(r);
        // La existencia se busca en la coleccion del rol (coordinadores), jamas en la de estudiantes.
        Assert.Equal(["GET coordinador EST-001"], f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "AUTH-09.a")]
    public async Task Estudiante_con_id_de_coordinador_responde_403_identidad()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(Usuario("estudiante", "COORD-01")));

        await Afirmar403IdentidadAsync(r);
        Assert.Equal(["GET estudiante COORD-01"], f.Fuente.Llamadas);
    }

    public static TheoryData<string> UsuariosConRolInvalido() => new()
    {
        "{\"rol\":\"admin\",\"id\":\"COORD-01\"}",
        "{\"rol\":\"rector\",\"id\":\"COORD-01\"}",
        "{\"rol\":\"Coordinador\",\"id\":\"COORD-01\"}",
        "{\"rol\":\"COORDINADOR\",\"id\":\"COORD-01\"}",
        "{\"rol\":\"\",\"id\":\"COORD-01\"}",
        "{\"rol\":null,\"id\":\"COORD-01\"}",
        "{\"rol\":123,\"id\":\"COORD-01\"}",
        "{\"rol\":true,\"id\":\"COORD-01\"}",
        "{\"rol\":[\"coordinador\"],\"id\":\"COORD-01\"}",
        "{\"id\":\"COORD-01\"}",
    };

    [Theory]
    [MemberData(nameof(UsuariosConRolInvalido))]
    [Trait("Criterio", "AUTH-10.a")]
    [Trait("Criterio", "AUTH-10.b")]
    public async Task Rol_fuera_de_lista_blanca_o_de_tipo_invalido_responde_403_identidad(string usuarioJson)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(usuarioJson));

        await Afirmar403IdentidadAsync(r);
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Theory]
    [InlineData("{\"rol\":\"estudiante\"}")]
    [InlineData("{\"rol\":\"estudiante\",\"id\":null}")]
    [InlineData("{\"rol\":\"estudiante\",\"id\":42}")]
    [InlineData("{\"rol\":\"estudiante\",\"id\":[\"EST-001\"]}")]
    [InlineData("{\"rol\":\"estudiante\",\"id\":\"\"}")]
    [Trait("Criterio", "AUTH-10.a")]
    public async Task Id_ausente_nulo_de_otro_tipo_o_vacio_responde_403_identidad(string usuarioJson)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(usuarioJson));

        await Afirmar403IdentidadAsync(r);
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "AUTH-11.a")]
    public async Task Id_inexistente_responde_403_identidad_con_cuerpo_identico_al_de_AUTH_09()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var referencia = await Afirmar403IdentidadAsync(
            await PostAsync(http, ConUsuario(Usuario("estudiante", "COORD-01"))));
        var estudiante = await Afirmar403IdentidadAsync(
            await PostAsync(http, ConUsuario(Usuario("estudiante", "EST-999"))));
        var coordinador = await Afirmar403IdentidadAsync(
            await PostAsync(http, ConUsuario(Usuario("coordinador", "COORD-99"))));

        Assert.Equal(referencia, estudiante);
        Assert.Equal(referencia, coordinador);
    }

    [Theory]
    [InlineData("est-001")]
    [InlineData(" EST-001")]
    [InlineData("EST-001 ")]
    [InlineData("EST-0001")]
    [Trait("Criterio", "AUTH-12.a")]
    public async Task Id_con_mayusculas_o_espacios_alterados_responde_403_identidad(string id)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(Usuario("estudiante", id)));

        await Afirmar403IdentidadAsync(r);
    }

    public static TheoryData<string> IdsDeInyeccionDeRuta() => new()
    {
        "EST-001/../EST-002",
        "EST-002%2F",
        "EST-001/notas",
        "EST-001?x=1",
        "../coordinadores/COORD-01",
    };

    [Theory]
    [MemberData(nameof(IdsDeInyeccionDeRuta))]
    [Trait("Criterio", "AUTH-13.a")]
    [Trait("Criterio", "AUTH-13.b")]
    public async Task Id_con_path_traversal_responde_403_identidad_sin_tocar_la_fuente(string id)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        foreach (var rol in new[] { "estudiante", "coordinador" })
        {
            var r = await PostAsync(http, ConUsuario(Usuario(rol, id)));
            await Afirmar403IdentidadAsync(r);
        }

        // AUTH-13.b: la fuente de datos falsa no recibe NINGUNA llamada con esos valores.
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Theory]
    [InlineData("EST‑001")]
    [InlineData("ЕST-001")]
    [Trait("Criterio", "AUTH-14.a")]
    public async Task Id_con_unicode_confundible_responde_403_identidad(string id)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(Usuario("estudiante", id)));

        await Afirmar403IdentidadAsync(r);
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "AUTH-15.c")]
    public async Task Usuario_duplicado_en_la_raiz_responde_400()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var json = "{\"pregunta\":\"¿Cuál es mi promedio?\","
            + "\"usuario\":{\"rol\":\"estudiante\",\"id\":\"EST-001\"},"
            + "\"usuario\":{\"rol\":\"coordinador\",\"id\":\"COORD-02\"}}";
        var r = await PostAsync(http, json);

        await AfirmarProblema(r, HttpStatusCode.BadRequest);
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Contrato", "SPEC-3.2")]
    public async Task Campos_extra_y_capitalizacion_distinta_se_ignoran_y_no_cambian_la_identidad()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var json = "{\"pregunta\":\"¿Cuál es mi promedio?\","
            + "\"usuario\":{\"rol\":\"estudiante\",\"id\":\"EST-001\",\"programa\":\"ADM\",\"esAdmin\":true},"
            + "\"estudianteId\":\"EST-002\",\"Usuario\":{\"rol\":\"coordinador\",\"id\":\"COORD-02\"}}";
        var r = await PostAsync(http, json);

        // Pasa L1 y el roster; el resto del flujo esta pendiente (503 explicito). Solo se valido EST-001.
        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(["GET estudiante EST-001"], f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.a")]
    public async Task Fuente_caida_al_validar_el_coordinador_responde_503_no_200_ni_403()
    {
        using var f = new FabricaApi();
        f.Fuente.Fallo = llamada => llamada == "GET coordinador COORD-01"
            ? new TutorFlow.Application.Errores.DependenciaNoDisponibleException("500 simulado")
            : null;
        var http = f.CreateClient();

        var r = await PostAsync(http, ConUsuario(Usuario("coordinador", "COORD-01")));

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.b")]
    public async Task Roster_ausente_o_vencido_responde_503_en_preguntas_y_la_salud_sigue_en_200()
    {
        using var f = new FabricaApi();
        f.Roster.Caido = true;
        var http = f.CreateClient();

        var pregunta = await PostAsync(http, ConUsuario(Usuario("estudiante", "EST-001")));
        var salud = await http.GetAsync("/api/salud");

        await AfirmarProblema(pregunta, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        Assert.Equal("{\"estado\":\"ok\"}", await salud.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Criterio", "RES-04.a")]
    public async Task Fuente_con_timeout_responde_503_problem_json_en_menos_de_10_segundos()
    {
        using var f = new FabricaApi(services =>
        {
            // Adaptador REAL con un manejador que nunca responde: el timeout lo corta (SPEC 10.2).
            var http = new HttpClient(new ColgadoHandler()) { BaseAddress = new Uri("http://mock-api.test/") };
            var opciones = new TutorFlow.Infrastructure.MockApi.MockApiOptions
            {
                BaseUrl = "http://mock-api.test",
                Timeout = TimeSpan.FromMilliseconds(100),
                Reintentos = 2,
                BackoffBase = TimeSpan.FromMilliseconds(10),
            };
            services.AddSingleton<TutorFlow.Application.Ports.IAcademicDataSource>(
                new TutorFlow.Infrastructure.MockApi.MockApiAcademicDataSource(http, opciones));
        });
        var cliente = f.CreateClient();

        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var r = await PostAsync(cliente, ConUsuario(Usuario("estudiante", "EST-001")));
        reloj.Stop();

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(10), $"tardo {reloj.Elapsed}");
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    [Trait("Criterio", "RES-09.a")]
    public async Task Excepcion_no_controlada_responde_500_problem_json_con_requestId_sin_filtrar_internos()
    {
        const string Marca = "SECRETO-INTERNO-/var/app/Config.cs";
        using var f = new FabricaApi();
        f.Fuente.Fallo = _ => new InvalidOperationException(Marca);
        var http = f.CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/preguntar")
        {
            Content = FabricaApi.Json(ConUsuario(Usuario("estudiante", "EST-001"))),
        };
        peticion.Headers.Add("X-Request-Id", "req-res09");

        var r = await http.SendAsync(peticion);

        await AfirmarProblema(r, HttpStatusCode.InternalServerError);
        var texto = await r.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(texto);
        Assert.Equal("req-res09", doc.RootElement.GetProperty("requestId").GetString());
        Assert.DoesNotContain(Marca, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("TutorFlow", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".cs", texto, StringComparison.Ordinal);
    }

    private sealed class ColgadoHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("inalcanzable");
        }
    }
}
