using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;

using TutorFlow.Application.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>
/// Base de los adaptadores HTTP: timeout de 20 s por intento, 2 reintentos ante 429/5xx/red respetando retry-after,
/// sin reintento ante 401/403. La llave solo viaja en la cabecera; nunca en logs ni en excepciones.
/// El HttpClient lo aporta Infrastructure (o las pruebas con respuestas grabadas).
/// </summary>
public abstract class HttpLlmClientBase : ILlmClient
{
    public const int MaxReintentos = 2;
    public static readonly TimeSpan TimeoutPorIntento = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan TopeRetryAfter = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    protected HttpLlmClientBase(
        HttpClient http,
        LlmOptions options,
        Action<string>? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log;
        _delay = delay ?? Task.Delay;
    }

    protected LlmOptions Options { get; }

    protected abstract string Endpoint { get; }

    protected abstract string BuildBody(LlmRequest solicitud);

    protected abstract void ApplyAuth(HttpRequestMessage request, string apiKey);

    protected abstract LlmResponse ParseResponse(string json);

    public async Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            _log?.Invoke("Configuracion LLM: LLM_API_KEY no esta definida.");
            throw new LlmNoDisponibleException("LLM_API_KEY no esta configurada.");
        }

        var body = BuildBody(solicitud);
        int? ultimoEstado = null;
        for (var intento = 0; intento <= MaxReintentos; intento++)
        {
            TimeSpan? retryAfter = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                ApplyAuth(request, Options.ApiKey);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeoutPorIntento);
                using var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                ultimoEstado = status;
                var texto = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return ParseResponse(texto);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
                    {
                        throw new LlmNoDisponibleException($"Respuesta ilegible del proveedor '{Options.Provider}'.", status, ex);
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _log?.Invoke($"Error de configuracion LLM: el proveedor '{Options.Provider}' rechazo la credencial (HTTP {status}).");
                    throw new LlmNoDisponibleException($"El proveedor '{Options.Provider}' rechazo la credencial (HTTP {status}).", status);
                }

                if (status != 429 && status < 500)
                {
                    var detalle = DetalleError(texto);
                    _log?.Invoke($"El proveedor '{Options.Provider}' rechazo la solicitud (HTTP {status}).{(detalle is null ? string.Empty : " error.message: " + detalle)}");
                    throw new LlmNoDisponibleException($"El proveedor '{Options.Provider}' rechazo la solicitud (HTTP {status}).", status);
                }

                retryAfter = LeerRetryAfter(response.Headers);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                ultimoEstado = null;
                _log?.Invoke($"Timeout de {TimeoutPorIntento.TotalSeconds:0} s del proveedor '{Options.Provider}'.");
            }
            catch (HttpRequestException)
            {
                ultimoEstado = null;
                _log?.Invoke($"Fallo de red hacia el proveedor '{Options.Provider}'.");
            }

            if (intento < MaxReintentos)
            {
                var espera = retryAfter ?? TimeSpan.FromMilliseconds(500 * (intento + 1));
                _log?.Invoke($"Reintento {intento + 1}/{MaxReintentos} del proveedor '{Options.Provider}' en {espera.TotalSeconds:0.###} s.");
                await _delay(espera, ct).ConfigureAwait(false);
            }
        }

        throw new LlmNoDisponibleException(
            $"El proveedor '{Options.Provider}' no respondio tras {MaxReintentos + 1} intentos.",
            ultimoEstado);
    }

    private static readonly System.Text.RegularExpressions.Regex PatronLlave = new(
        @"(AIza[0-9A-Za-z_\-]{8,}|AQ\.[0-9A-Za-z_\-.]{8,}|sk-[0-9A-Za-z_\-]{8,}|(?<=key=)[^&\s""']+|(?<=Bearer\s)[0-9A-Za-z_\-.]{8,})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Extrae error.message del JSON del proveedor, enmascara llaves y trunca a 300 caracteres. Nunca el cuerpo de la peticion.</summary>
    private string? DetalleError(string cuerpoRespuesta)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpoRespuesta);
            var raiz = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? doc.RootElement[0]
                : doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("error", out var err)
                || err.ValueKind != JsonValueKind.Object
                || !err.TryGetProperty("message", out var msg)
                || msg.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var texto = PatronLlave.Replace(msg.GetString() ?? string.Empty, "***");
            if (!string.IsNullOrEmpty(Options.ApiKey))
            {
                texto = texto.Replace(Options.ApiKey, "***", StringComparison.Ordinal);
            }

            texto = texto.ReplaceLineEndings(" ");
            return texto.Length > 300 ? texto[..300] : texto;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeSpan? LeerRetryAfter(HttpResponseHeaders headers)
    {
        var ra = headers.RetryAfter;
        if (ra is null)
        {
            return null;
        }

        TimeSpan? valor = ra.Delta ?? (ra.Date is { } d ? d - DateTimeOffset.UtcNow : null);
        if (valor is null)
        {
            return null;
        }

        if (valor < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return valor > TopeRetryAfter ? TopeRetryAfter : valor;
    }

    /// <summary>Convencion de mensajes: Asistente + InvocacionId = propuesta de herramienta (Contenido = argumentos JSON).</summary>
    protected static bool EsInvocacionAsistente(LlmMensaje m) =>
        m.Rol == RolMensaje.Asistente && m.InvocacionId is not null && m.NombreHerramienta is not null;

    protected static JsonElement ArgumentosComoJson(string contenido)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(contenido) ? "{}" : contenido);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }
    }
}
