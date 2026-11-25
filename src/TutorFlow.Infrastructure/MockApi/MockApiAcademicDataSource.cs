using System.Globalization;
using System.Net;
using System.Text.Json;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Academico;

namespace TutorFlow.Infrastructure.MockApi;

/// <summary>
/// Adaptador de la API simulada (SPEC 10.2). Solo GET; timeout por intento; reintentos con backoff ante
/// timeout, 5xx o error de red; 404 devuelve null (o lista vacia). Los ids siempre van con URL-encoding.
/// JSON con tipos inesperados lanza DatoAcademicoInvalidoException: no se inventan ni recortan valores.
/// </summary>
public sealed class MockApiAcademicDataSource : IAcademicDataSource
{
    private readonly HttpClient _http;
    private readonly MockApiOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public MockApiAcademicDataSource(HttpClient http, MockApiOptions options, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _delay = delay ?? Task.Delay;
    }

    public async Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(estudianteId, "estudiantes/{0}", ct);
        return doc is null ? null : LeerEstudiante(doc.RootElement);
    }

    public async Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(estudianteId, "estudiantes/{0}/notas", ct);
        return doc is null ? [] : LeerLista(doc.RootElement, LeerNota);
    }

    public async Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(estudianteId, "estudiantes/{0}/asistencia", ct);
        return doc is null ? [] : LeerLista(doc.RootElement, LeerAsistencia);
    }

    public async Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(coordinadorId, "coordinadores/{0}", ct);
        return doc is null ? null : LeerCoordinador(doc.RootElement);
    }

    public async Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync(null, "programas", ct);
        return doc is null ? [] : LeerLista(doc.RootElement, LeerPrograma);
    }

    public async Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(programaId, "programas/{0}/estudiantes", ct);
        return doc is null ? [] : LeerLista(doc.RootElement, LeerEstudiante);
    }

    /// <summary>
    /// Codifica el id (Uri.EscapeDataString). "." y ".." no cambian al codificarse y System.Uri los colapsaria
    /// como segmentos de ruta (apuntarian a otro recurso): se tratan como inexistentes sin llamar a la red.
    /// </summary>
    private static string? Enc(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id is "" or "." or ".." ? null : Uri.EscapeDataString(id);
    }

    // ---------- HTTP ----------

    private async Task<JsonDocument?> GetJsonAsync(string? id, string plantilla, CancellationToken ct)
    {
        var relativePath = plantilla;
        if (id is not null)
        {
            var enc = Enc(id);
            if (enc is null)
            {
                return null;
            }

            relativePath = plantilla.Replace("{0}", enc, StringComparison.Ordinal);
        }

        var intentos = Math.Max(0, _options.Reintentos) + 1;
        Exception? ultimo = null;
        for (var intento = 0; intento < intentos; intento++)
        {
            if (intento > 0)
            {
                await _delay(TimeSpan.FromTicks(_options.BackoffBase.Ticks * (1L << (intento - 1))), ct);
            }

            ct.ThrowIfCancellationRequested();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.Timeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(relativePath, UriKind.Relative));
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if ((int)response.StatusCode >= 500)
                {
                    ultimo = new HttpRequestException($"La API simulada respondio {(int)response.StatusCode}.");
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // 4xx distinto de 404: no se reintenta; fail-closed.
                    throw new DependenciaNoDisponibleException($"La API simulada respondio {(int)response.StatusCode}.");
                }

                var body = await response.Content.ReadAsStringAsync(cts.Token);
                try
                {
                    return JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    throw new DatoAcademicoInvalidoException($"Respuesta de la API simulada no es JSON valido: {ex.Message}");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                ultimo = new TimeoutException("Timeout de la API simulada.", ex);
            }
            catch (HttpRequestException ex)
            {
                ultimo = ex;
            }
        }

        throw new DependenciaNoDisponibleException(
            $"La API simulada no responde tras {intentos} intentos.",
            ultimo ?? new InvalidOperationException());
    }

    // ---------- Lectura estricta ----------

    private static IReadOnlyList<T> LeerLista<T>(JsonElement raiz, Func<JsonElement, T> lector)
    {
        if (raiz.ValueKind != JsonValueKind.Array)
        {
            throw Invalido("se esperaba un arreglo");
        }

        var lista = new List<T>(raiz.GetArrayLength());
        foreach (var item in raiz.EnumerateArray())
        {
            lista.Add(lector(item));
        }

        return lista;
    }

    private static EstudianteFuente LeerEstudiante(JsonElement e)
    {
        Objeto(e);
        return new EstudianteFuente(
            Texto(e, "id"),
            Texto(e, "nombre"),
            Texto(e, "programaId"),
            EnteroOpcional(e, "semestre"));
    }

    private static CoordinadorFuente LeerCoordinador(JsonElement e)
    {
        Objeto(e);
        if (!e.TryGetProperty("programas", out var progs) || progs.ValueKind != JsonValueKind.Array)
        {
            throw Invalido("coordinador.programas debe ser un arreglo");
        }

        var lista = new List<string>();
        foreach (var p in progs.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(p.GetString()))
            {
                throw Invalido("coordinador.programas debe contener textos no vacios");
            }

            lista.Add(p.GetString()!);
        }

        return new CoordinadorFuente(Texto(e, "id"), Texto(e, "nombre"), lista);
    }

    private static ProgramaFuente LeerPrograma(JsonElement e)
    {
        Objeto(e);
        string? coord = null;
        if (e.TryGetProperty("coordinadorId", out var c) && c.ValueKind != JsonValueKind.Null)
        {
            coord = Texto(e, "coordinadorId");
        }

        return new ProgramaFuente(Texto(e, "id"), Texto(e, "nombre"), coord);
    }

    private static NotaFuente LeerNota(JsonElement e)
    {
        Objeto(e);
        return new NotaFuente(
            Texto(e, "estudianteId"),
            Texto(e, "codigo"),
            Texto(e, "asignatura"),
            EnteroOpcional(e, "creditos") ?? throw Invalido("'creditos' es obligatorio"),
            DecimalOpcional(e, "corte1"),
            DecimalOpcional(e, "corte2"),
            DecimalOpcional(e, "corte3"));
    }

    private static AsistenciaFuente LeerAsistencia(JsonElement e)
    {
        Objeto(e);
        return new AsistenciaFuente(
            Texto(e, "estudianteId"),
            Texto(e, "codigo"),
            Texto(e, "asignatura"),
            EnteroOpcional(e, "clasesProgramadas"),
            EnteroOpcional(e, "inasistencias"));
    }

    private static void Objeto(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw Invalido("se esperaba un objeto");
        }
    }

    private static string Texto(JsonElement e, string campo)
    {
        if (!e.TryGetProperty(campo, out var v) || v.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(v.GetString()))
        {
            throw Invalido($"'{campo}' debe ser un texto no vacio");
        }

        return v.GetString()!;
    }

    private static int? EnteroOpcional(JsonElement e, string campo)
    {
        if (!e.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var n))
        {
            throw Invalido($"'{campo}' debe ser un entero");
        }

        return n;
    }

    private static decimal? DecimalOpcional(JsonElement e, string campo)
    {
        if (!e.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (v.ValueKind != JsonValueKind.Number
            || !decimal.TryParse(v.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            throw Invalido($"'{campo}' debe ser un numero");
        }

        return d;
    }

    private static DatoAcademicoInvalidoException Invalido(string detalle)
    {
        return new DatoAcademicoInvalidoException($"Dato invalido de la API simulada: {detalle}.");
    }
}
