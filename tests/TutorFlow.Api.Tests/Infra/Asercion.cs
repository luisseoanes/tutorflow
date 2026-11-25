using System.Net;
using System.Text.Json;

namespace TutorFlow.Api.Tests.Infra;

internal static class Asercion
{
    public const string MensajeIdentidad = "No fue posible validar la identidad indicada en la solicitud.";

    public static string ConUsuario(string usuarioJson, string pregunta = "¿Cuál es mi promedio?") =>
        "{\"pregunta\":" + JsonSerializer.Serialize(pregunta) + ",\"usuario\":" + usuarioJson + "}";

    public static string Usuario(string rol, string id) =>
        "{\"rol\":" + JsonSerializer.Serialize(rol) + ",\"id\":" + JsonSerializer.Serialize(id) + "}";

    public static async Task<HttpResponseMessage> PostAsync(HttpClient http, string json, string mediaType = "application/json") =>
        await http.PostAsync("/api/asistente/preguntar", FabricaApi.Json(json, mediaType));

    /// <summary>403-identidad (authorization.md): 403, citas [], usoDatosEstudiante false, mensaje de SPEC 3.4, tokens 0.</summary>
    public static async Task<string> Afirmar403IdentidadAsync(HttpResponseMessage r)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using var doc = JsonDocument.Parse(texto);
        var raiz = doc.RootElement;
        Assert.Equal(
            new[] { "respuesta", "citas", "usoDatosEstudiante", "tokens", "costoUsd" },
            raiz.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(MensajeIdentidad, raiz.GetProperty("respuesta").GetString());
        Assert.Equal(0, raiz.GetProperty("citas").GetArrayLength());
        Assert.False(raiz.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(0, raiz.GetProperty("tokens").GetInt32());
        Assert.Equal(0m, raiz.GetProperty("costoUsd").GetDecimal());
        return texto;
    }

    public static async Task AfirmarProblema(HttpResponseMessage r, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal((int)esperado, doc.RootElement.GetProperty("status").GetInt32());
    }
}
