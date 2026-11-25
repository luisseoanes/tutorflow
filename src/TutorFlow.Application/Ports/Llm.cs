using System.Text.Json;

namespace TutorFlow.Application.Ports;

/// <summary>Definicion de herramienta que ve el LLM (esquema JSON de sus argumentos).</summary>
public sealed record ToolDefinition(string Nombre, string Descripcion, JsonElement EsquemaArgumentos);

/// <summary>Propuesta de invocacion hecha por el LLM. Es solo una propuesta: el despachador la valida y autoriza.</summary>
public sealed record InvocacionPropuesta(string? Id, string Nombre, JsonElement Argumentos);

public enum RolMensaje
{
    Usuario,
    Asistente,
    Herramienta,
}

public sealed record LlmMensaje(
    RolMensaje Rol,
    string Contenido,
    string? InvocacionId = null,
    string? NombreHerramienta = null);

public sealed record LlmRequest(
    string Sistema,
    IReadOnlyList<LlmMensaje> Mensajes,
    IReadOnlyList<ToolDefinition> Herramientas,
    int MaxTokens);

public sealed record UsoTokens(int Entrada, int Salida);

public sealed record LlmResponse(
    string Texto,
    IReadOnlyList<InvocacionPropuesta> Invocaciones,
    UsoTokens Uso);

/// <summary>Puerto del proveedor LLM. Su salida es texto o propuestas; nunca decide permisos.</summary>
public interface ILlmClient
{
    Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct);
}

public interface IEmbeddingClient
{
    Task<IReadOnlyList<float[]>> VectorizarAsync(IReadOnlyList<string> textos, CancellationToken ct);
}
