using System.Net;
using System.Text;
using System.Text.Json;
using TutorFlow.Api.Tests.Infra;
using static TutorFlow.Api.Tests.Infra.Asercion;

namespace TutorFlow.Api.Tests.Cuerpo;

/// <summary>Lectura del cuerpo y salud (SPEC 3.2, 3.3 reglas 1-4).</summary>
public class CuerpoApiTests
{
    private const string Marca = "MARCA-ECO-7731";
    private const string UsuarioOk = "{\"rol\":\"estudiante\",\"id\":\"EST-001\"}";

    /// <summary>Casos de ADV-16.a: (nombre, cuerpo, content-type). Todos deben ser 400.</summary>
    public static TheoryData<string, string, string> Casos400() => new()
    {
        { "json-malformado", "{\"pregunta\":\"" + Marca + "\"", "application/json" },
        { "cuerpo-vacio", string.Empty, "application/json" },
        { "raiz-array", "[\"" + Marca + "\"]", "application/json" },
        { "content-type-text-plain", "{\"pregunta\":\"" + Marca + "\",\"usuario\":" + UsuarioOk + "}", "text/plain" },
        { "mayor-de-64KB", "{\"pregunta\":\"" + Marca + "\",\"relleno\":\"" + new string('x', 70_000) + "\",\"usuario\":" + UsuarioOk + "}", "application/json" },
        { "pregunta-null", "{\"pregunta\":null,\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
        { "pregunta-numero", "{\"pregunta\":123,\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
        { "usuario-string", "{\"pregunta\":\"" + Marca + "\",\"usuario\":\"EST-001\"}", "application/json" },
        { "usuario-null", "{\"pregunta\":\"" + Marca + "\",\"usuario\":null}", "application/json" },
        { "usuario-duplicado", "{\"pregunta\":\"" + Marca + "\",\"usuario\":" + UsuarioOk + ",\"usuario\":" + UsuarioOk + "}", "application/json" },
        { "id-duplicado-en-usuario", "{\"pregunta\":\"" + Marca + "\",\"usuario\":{\"rol\":\"estudiante\",\"id\":\"EST-001\",\"id\":\"EST-002\"}}", "application/json" },
        { "pregunta-duplicada", "{\"pregunta\":\"a\",\"pregunta\":\"" + Marca + "\",\"usuario\":" + UsuarioOk + "}", "application/json" },
        { "duplicado-con-escape", "{\"pregunta\":\"" + Marca + "\",\"usuario\":" + UsuarioOk + ",\"usuari\\u006f\":" + UsuarioOk + "}", "application/json" },
        { "pregunta-ausente", "{\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
        { "pregunta-vacia", "{\"pregunta\":\"\",\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
        { "pregunta-solo-espacios", "{\"pregunta\":\"  \\t \",\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
        { "usuario-ausente", "{\"pregunta\":\"" + Marca + "\"}", "application/json" },
        { "usuario-array", "{\"pregunta\":\"" + Marca + "\",\"usuario\":[\"EST-001\"]}", "application/json" },
        { "usuario-numero", "{\"pregunta\":\"" + Marca + "\",\"usuario\":7}", "application/json" },
        { "pregunta-1001", "{\"pregunta\":\"" + new string('p', 1001) + "\",\"usuario\":" + UsuarioOk + ",\"x\":\"" + Marca + "\"}", "application/json" },
    };

    [Theory]
    [MemberData(nameof(Casos400))]
    [Trait("Criterio", "ADV-16.a")]
    [Trait("Criterio", "ADV-16.b")]
    public async Task Cuerpo_malformado_responde_400_problem_json_sin_eco_ni_500(string nombre, string cuerpo, string contentType)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var r = await PostAsync(http, cuerpo, contentType);
        var texto = await r.Content.ReadAsStringAsync();

        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{nombre}: {(int)r.StatusCode}");
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(texto);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        // ADV-16.b: sin eco de la entrada.
        Assert.DoesNotContain(Marca, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("EST-001", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 20), texto, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('p', 20), texto, StringComparison.Ordinal);
        // Ningun 400 toca la fuente de datos.
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "ADV-16.a")]
    public async Task Cuerpo_de_64KB_exactos_no_se_rechaza_por_tamano_pero_uno_mayor_si()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();
        var prefijo = "{\"pregunta\":\"hola\",\"usuario\":" + UsuarioOk + ",\"r\":\"";
        const string Sufijo = "\"}";
        var relleno = 64 * 1024 - Encoding.UTF8.GetByteCount(prefijo) - Sufijo.Length;

        var justo = await PostAsync(http, prefijo + new string('a', relleno) + Sufijo);
        var excedido = await PostAsync(http, prefijo + new string('a', relleno + 1) + Sufijo);

        Assert.NotEqual(HttpStatusCode.BadRequest, justo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, excedido.StatusCode);
    }

    [Theory]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("APPLICATION/JSON")]
    [Trait("Criterio", "ADV-16.a")]
    public async Task Content_Type_json_con_parametros_se_acepta(string contentType)
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/preguntar")
        {
            Content = new StringContent(ConUsuario(UsuarioOk), Encoding.UTF8),
        };
        peticion.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);

        var r = await http.SendAsync(peticion);

        Assert.NotEqual(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    [Trait("Criterio", "ADV-15.a")]
    public async Task Pregunta_de_1001_caracteres_responde_400_y_una_de_1000_no()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var larga = await PostAsync(http, ConUsuario(UsuarioOk, new string('a', 1001)));
        var limite = await PostAsync(http, ConUsuario(UsuarioOk, new string('a', 1000)));

        await AfirmarProblema(larga, HttpStatusCode.BadRequest);
        Assert.NotEqual(HttpStatusCode.BadRequest, limite.StatusCode);
    }

    [Fact]
    [Trait("Criterio", "ADV-16.a")]
    public async Task Las_reglas_se_evaluan_en_orden_y_gana_la_primera_que_aplica()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        // Regla 2 antes que 3: duplicado en usuario con pregunta invalida -> 400 (ambas son 400; se distingue por detalle).
        var r2 = await DetalleAsync(http, "{\"pregunta\":\"\",\"usuario\":{\"id\":\"a\",\"id\":\"b\"}}");
        var r3 = await DetalleAsync(http, "{\"pregunta\":\"\",\"usuario\":null}");
        var r4 = await DetalleAsync(http, "{\"pregunta\":\"hola\",\"usuario\":null}");

        Assert.Contains("duplicadas", r2, StringComparison.Ordinal);
        Assert.Contains("'pregunta'", r3, StringComparison.Ordinal);
        Assert.Contains("'usuario'", r4, StringComparison.Ordinal);

        // Regla 4 antes que 5: usuario no objeto es 400; usuario objeto sin rol ni id es 403.
        var r5 = await PostAsync(http, "{\"pregunta\":\"hola\",\"usuario\":{}}");
        await Afirmar403IdentidadAsync(r5);
    }

    [Fact]
    [Trait("Criterio", "RES-01.a")]
    public async Task Salud_responde_200_con_el_cuerpo_exacto_aunque_la_fuente_y_el_roster_fallen()
    {
        using var f = new FabricaApi();
        f.Fuente.Fallo = _ => new TutorFlow.Application.Errores.DependenciaNoDisponibleException("caida");
        f.Roster.Caido = true;
        var http = f.CreateClient();

        var r = await http.GetAsync("/api/salud");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("{\"estado\":\"ok\"}", await r.Content.ReadAsStringAsync());
        Assert.Equal("application/json; charset=utf-8", r.Content.Headers.ContentType?.ToString());
        Assert.Empty(f.Fuente.Llamadas);
    }

    [Fact]
    [Trait("Observabilidad", "X-Request-Id")]
    public async Task X_Request_Id_valido_se_devuelve_y_uno_invalido_se_reemplaza()
    {
        using var f = new FabricaApi();
        var http = f.CreateClient();

        var valido = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        valido.Headers.Add("X-Request-Id", "abc-123");
        var invalido = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        invalido.Headers.Add("X-Request-Id", "no valido!");
        var sin = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        var largo = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        largo.Headers.Add("X-Request-Id", new string('a', 65));

        Assert.Equal("abc-123", (await http.SendAsync(valido)).Headers.GetValues("X-Request-Id").Single());
        var reemplazado = (await http.SendAsync(invalido)).Headers.GetValues("X-Request-Id").Single();
        Assert.NotEqual("no valido!", reemplazado);
        Assert.Matches("^[A-Za-z0-9-]{1,64}$", reemplazado);
        Assert.Matches("^[A-Za-z0-9-]{1,64}$", (await http.SendAsync(sin)).Headers.GetValues("X-Request-Id").Single());
        Assert.NotEqual(new string('a', 65), (await http.SendAsync(largo)).Headers.GetValues("X-Request-Id").Single());
    }

    private static async Task<string> DetalleAsync(HttpClient http, string json)
    {
        var r = await PostAsync(http, json);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString()!;
    }
}
