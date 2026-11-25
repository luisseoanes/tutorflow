using System.Text.Json.Serialization;

namespace TutorFlow.Api.Contracts;

/// <summary>Cita del contrato (SPEC 3.1): exactamente <c>documento</c> y <c>seccion</c>.</summary>
public sealed record CitaDto(
    [property: JsonPropertyName("documento")] string Documento,
    [property: JsonPropertyName("seccion")] string Seccion);

/// <summary>Respuesta de POST /api/asistente/preguntar: 5 propiedades y ninguna otra.</summary>
public sealed record PreguntarResponse(
    [property: JsonPropertyName("respuesta")] string Respuesta,
    [property: JsonPropertyName("citas")] IReadOnlyList<CitaDto> Citas,
    [property: JsonPropertyName("usoDatosEstudiante")] bool UsoDatosEstudiante,
    [property: JsonPropertyName("tokens")] int Tokens,
    [property: JsonPropertyName("costoUsd")] decimal CostoUsd);

/// <summary>Respuesta de GET /api/salud: exactamente <c>{"estado":"ok"}</c>.</summary>
public sealed record SaludResponse(
    [property: JsonPropertyName("estado")] string Estado);
