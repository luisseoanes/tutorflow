using System.Security.Cryptography;

namespace TutorFlow.Api.Http;

/// <summary>
/// X-Request-Id (SPEC 10.4): se acepta si cumple <c>[A-Za-z0-9-]{1,64}</c>; si no, se genera. Se devuelve en la
/// respuesta y queda disponible para los logs (alcance del logger y <c>HttpContext.Items</c>).
/// </summary>
public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "TutorFlow.RequestId";
    private const int MaxLongitud = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var entrante = context.Request.Headers[HeaderName].ToString();
        var id = EsValido(entrante) ? entrante : Generar();
        context.Items[ItemKey] = id;
        context.Response.Headers[HeaderName] = id;

        using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
        {
            await _next(context);
        }
    }

    public static string Obtener(HttpContext context) =>
        context.Items[ItemKey] as string ?? Generar();

    public static bool EsValido(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length > MaxLongitud)
        {
            return false;
        }

        foreach (var c in valor)
        {
            var ok = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static string Generar() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
