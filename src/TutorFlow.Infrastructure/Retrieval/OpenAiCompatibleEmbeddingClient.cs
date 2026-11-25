using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Retrieval;

namespace TutorFlow.Infrastructure.Retrieval;

/// <summary>
/// Adaptador del endpoint /embeddings (OpenAI y compatibles). Mismas reglas de resiliencia que el LLM: 20 s por intento,
/// 2 reintentos ante 429/5xx/red, sin reintento ante 401/403. La llave solo viaja en la cabecera Authorization.
/// Acumula tokens (usage.prompt_tokens/total_tokens) y costo (EMBEDDING_PRICE_PER_MTOK).
/// </summary>
public sealed class OpenAiCompatibleEmbeddingClient : IEmbeddingClient, IEmbeddingUsageReporter
{
    public const int MaxReintentos = 2;
    public static readonly TimeSpan TimeoutPorIntento = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;
    private readonly EmbeddingOptions _options;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private long _tokens;

    public OpenAiCompatibleEmbeddingClient(
        HttpClient http,
        EmbeddingOptions options,
        Action<string>? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log;
        _delay = delay ?? Task.Delay;
    }

    public long Tokens => Interlocked.Read(ref _tokens);

    public decimal CostoUsd => Tokens / 1_000_000m * _options.PrecioPorMtok;

    private string Endpoint => (_options.BaseUrl ?? EmbeddingOptions.DefaultBaseUrl).TrimEnd('/') + "/embeddings";

    public async Task<IReadOnlyList<float[]>> VectorizarAsync(IReadOnlyList<string> textos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(textos);
        if (textos.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _log?.Invoke("Configuracion de embeddings: EMBEDDING_API_KEY no esta definida.");
            throw new LlmNoDisponibleException("EMBEDDING_API_KEY no esta configurada.");
        }

        var body = JsonSerializer.Serialize(new { model = _options.Model, input = textos });
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
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
                        return Parsear(texto, textos.Count);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
                    {
                        throw new LlmNoDisponibleException("Respuesta ilegible del proveedor de embeddings.", status, ex);
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _log?.Invoke($"Error de configuracion de embeddings: credencial rechazada (HTTP {status}).");
                    throw new LlmNoDisponibleException($"El proveedor de embeddings rechazo la credencial (HTTP {status}).", status);
                }

                if (status != 429 && status < 500)
                {
                    _log?.Invoke($"El proveedor de embeddings rechazo la solicitud (HTTP {status}).");
                    throw new LlmNoDisponibleException($"El proveedor de embeddings rechazo la solicitud (HTTP {status}).", status);
                }

                retryAfter = response.Headers.RetryAfter?.Delta;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                ultimoEstado = null;
                _log?.Invoke($"Timeout de {TimeoutPorIntento.TotalSeconds:0} s del proveedor de embeddings.");
            }
            catch (HttpRequestException)
            {
                ultimoEstado = null;
                _log?.Invoke("Fallo de red hacia el proveedor de embeddings.");
            }

            if (intento < MaxReintentos)
            {
                var espera = retryAfter is { } r && r >= TimeSpan.Zero ? (r > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : r) : TimeSpan.FromMilliseconds(500 * (intento + 1));
                _log?.Invoke($"Reintento {intento + 1}/{MaxReintentos} del proveedor de embeddings en {espera.TotalSeconds:0.###} s.");
                await _delay(espera, ct).ConfigureAwait(false);
            }
        }

        throw new LlmNoDisponibleException($"El proveedor de embeddings no respondio tras {MaxReintentos + 1} intentos.", ultimoEstado);
    }

    private IReadOnlyList<float[]> Parsear(string json, int esperados)
    {
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var items = new List<(int Index, float[] Vector)>();
        var pos = 0;
        foreach (var d in data.EnumerateArray())
        {
            var index = d.TryGetProperty("index", out var ix) ? ix.GetInt32() : pos;
            var v = d.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
            items.Add((index, v));
            pos++;
        }

        if (items.Count != esperados)
        {
            throw new InvalidOperationException("Cantidad de embeddings inesperada.");
        }

        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            long t = 0;
            if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
            {
                t = pt.GetInt64();
            }
            else if (usage.TryGetProperty("total_tokens", out var tt) && tt.ValueKind == JsonValueKind.Number)
            {
                t = tt.GetInt64();
            }

            Interlocked.Add(ref _tokens, t);
        }

        return items.OrderBy(i => i.Index).Select(i => i.Vector).ToList();
    }
}

/// <summary>Cache en un archivo JSON (clave -> vector). Ruta configurable; nunca dentro de src/ (lo valida la configuracion).</summary>
public sealed class JsonFileEmbeddingCache : IEmbeddingCache
{
    private readonly string _path;
    private readonly Dictionary<string, float[]> _data = new(StringComparer.Ordinal);
    private bool _sucio;

    public JsonFileEmbeddingCache(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        if (File.Exists(path))
        {
            try
            {
                var leido = JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(path, Encoding.UTF8));
                if (leido is not null)
                {
                    foreach (var kv in leido)
                    {
                        _data[kv.Key] = kv.Value;
                    }
                }
            }
            catch (JsonException)
            {
                // Cache corrupta: se reconstruye.
            }
        }
    }

    public float[]? Obtener(string clave) => _data.GetValueOrDefault(clave);

    public void Guardar(string clave, float[] vector)
    {
        _data[clave] = vector;
        _sucio = true;
    }

    public void Persistir()
    {
        if (!_sucio)
        {
            return;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_data), new UTF8Encoding(false));
        _sucio = false;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"JsonFileEmbeddingCache({_data.Count} entradas)");
}

/// <summary>Construccion de estrategias de recuperacion. Usable por la aplicacion y por el runner [ret] de Arnes.</summary>
public static class RetrieverFactory
{
    public static readonly IReadOnlyList<string> Estrategias = ["full-context", "bm25", "vector", "hybrid"];

    /// <summary>
    /// Crea la estrategia. vector/hybrid exigen EMBEDDING_* completo (lanza <see cref="EmbeddingConfiguracionException"/>).
    /// Ejemplo para el experimento: <c>RetrieverFactory.Crear("hybrid", catalog, EmbeddingOptions.FromConfig(Environment.GetEnvironmentVariable), new HttpClient { Timeout = Timeout.InfiniteTimeSpan })</c>.
    /// </summary>
    public static IRetriever Crear(string estrategia, IChunkCatalog catalog, EmbeddingOptions? embedding = null, HttpClient? http = null, Action<string>? log = null) =>
        CrearConUso(estrategia, catalog, embedding, http, log).Retriever;

    /// <summary>Igual que <see cref="Crear"/>, devolviendo ademas el reporte de tokens/costo de embeddings (null si no hay).</summary>
    public static (IRetriever Retriever, IEmbeddingUsageReporter? Uso) CrearConUso(string estrategia, IChunkCatalog catalog, EmbeddingOptions? embedding = null, HttpClient? http = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        switch (estrategia.Trim().ToLowerInvariant())
        {
            case "full-context":
                return (new FullContextRetriever(catalog), null);
            case "bm25":
                return (new Bm25Retriever(catalog), null);
            case "vector":
                var vec = CrearVector(estrategia, catalog, embedding, http, log, out var c1);
                return (vec, c1);
            case "hybrid":
                var v = CrearVector(estrategia, catalog, embedding, http, log, out var c2);
                return (new HybridRetriever(new Bm25Retriever(catalog), v), c2);
            default:
                throw new InvalidOperationException(
                    $"RETRIEVAL_STRATEGY '{estrategia}' no valida ({string.Join(" | ", Estrategias)}).");
        }
    }

    /// <summary>Valida la configuracion de la estrategia sin crear nada (para fallar al arrancar).</summary>
    public static void Validar(string estrategia, EmbeddingOptions embedding)
    {
        var e = estrategia.Trim().ToLowerInvariant();
        if (!Estrategias.Contains(e))
        {
            throw new InvalidOperationException($"RETRIEVAL_STRATEGY '{estrategia}' no valida ({string.Join(" | ", Estrategias)}).");
        }

        if (e is "vector" or "hybrid")
        {
            embedding.ExigirParaVectorial(e);
        }
    }

    private static VectorRetriever CrearVector(string estrategia, IChunkCatalog catalog, EmbeddingOptions? embedding, HttpClient? http, Action<string>? log, out OpenAiCompatibleEmbeddingClient cliente)
    {
        var opciones = embedding ?? new EmbeddingOptions();
        opciones.ExigirParaVectorial(estrategia);
        cliente = new OpenAiCompatibleEmbeddingClient(http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, opciones, log);
        IEmbeddingCache? cache = opciones.CachePath is { } p ? new JsonFileEmbeddingCache(p) : null;
        return new VectorRetriever(catalog, cliente, opciones.Model!, cache);
    }
}
