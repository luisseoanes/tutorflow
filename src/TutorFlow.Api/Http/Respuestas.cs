using System.Text.Json;
using System.Text.Json.Serialization;
using TutorFlow.Api.Contracts;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Api.Http;

/// <summary>Cuerpos de error de SPEC 3.3 y 3.4. Ningun cuerpo repite la entrada del cliente.</summary>
public static class Respuestas
{
    public const string ContentTypeJson = "application/json; charset=utf-8";
    public const string ContentTypeProblem = "application/problem+json; charset=utf-8";

    public const string MensajeIdentidad = "No fue posible validar la identidad indicada en la solicitud.";
    public const string MensajeAccesoEstudiante =
        "No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.";
    public const string MensajeAccesoCoordinador =
        "No tienes permiso para consultar esa información. Solo puedes consultar los datos de estudiantes de los programas que coordinas.";

    public static readonly JsonSerializerOptions Opciones = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PreguntarResponse Identidad() =>
        new(MensajeIdentidad, [], false, 0, 0m);

    public static PreguntarResponse Acceso(RolAcademico rol, int tokens = 0, decimal costoUsd = 0m) =>
        new(
            rol == RolAcademico.Coordinador ? MensajeAccesoCoordinador : MensajeAccesoEstudiante,
            [new CitaDto("reglamento_academico_2025.md", "Artículo 22")],
            false,
            tokens,
            costoUsd);

    public static Task EscribirJsonAsync<T>(HttpResponse response, int status, T cuerpo)
    {
        response.StatusCode = status;
        response.ContentType = ContentTypeJson;
        return JsonSerializer.SerializeAsync(response.Body, cuerpo, Opciones, response.HttpContext.RequestAborted);
    }

    public static Task EscribirProblemaAsync(HttpResponse response, int status, string titulo, string detalle, string requestId)
    {
        response.StatusCode = status;
        response.ContentType = ContentTypeProblem;
        var problema = new Problema("about:blank", titulo, status, detalle, requestId);
        return JsonSerializer.SerializeAsync(response.Body, problema, Opciones, response.HttpContext.RequestAborted);
    }

    /// <summary>RFC 7807 con el identificador de peticion. Sin stack trace, rutas, tipos ni configuracion.</summary>
    public sealed record Problema(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("detail")] string Detail,
        [property: JsonPropertyName("requestId")] string RequestId);
}
