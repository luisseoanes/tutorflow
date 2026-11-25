using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Audit;
using TutorFlow.Infrastructure.Roster;

namespace TutorFlow.Api.Tests.Infra;

/// <summary>Raiz del repositorio (donde esta TutorFlow.sln): de ahi salen el reglamento y los datos del material.</summary>
internal static class RutaRepo
{
    public static string Raiz { get; } = Buscar();

    public static string Reglamento => Path.Combine(Raiz, "q10", "material", "reglamento");

    private static string Buscar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TutorFlow.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontro TutorFlow.sln");
    }
}

/// <summary>Conjunto de datos del material (escenarios/academic-data.md). SOLO vive en tests/ (RES-12).</summary>
internal static class DatosMaterial
{
    private static readonly (string Cod, string Nombre, int Creditos)[] Ing =
    [
        ("CAL-201", "Cálculo Integral", 4), ("POO-202", "Programación Orientada a Objetos", 3),
        ("BD-203", "Bases de Datos", 3), ("ING-204", "Inglés III", 2),
    ];

    private static readonly (string Cod, string Nombre, int Creditos)[] Adm =
    [
        ("CON-101", "Contabilidad", 3), ("MIC-102", "Microeconomía", 3),
        ("EST-103", "Estadística", 4), ("COM-104", "Comunicación", 2),
    ];

    private static readonly Dictionary<string, (decimal?, decimal?, decimal?, int, int?)[]> Filas = new()
    {
        ["EST-001"] = [(4.2m, 4.5m, 4.6m, 64, 1), (4.8m, 4.6m, 4.9m, 48, 0), (4.0m, 3.8m, 4.4m, 48, 2), (3.9m, 4.1m, 4.0m, 32, 1)],
        ["EST-002"] = [(2.5m, 2.8m, 2.6m, 64, 3), (3.5m, 3.2m, 3.6m, 48, 2), (3.8m, 3.6m, 3.9m, 48, 9), (3.0m, 3.4m, 3.1m, 32, 1)],
        ["EST-003"] = [(4.0m, 3.9m, 4.2m, 64, 2), (3.7m, 4.0m, 3.8m, 48, 1), (4.1m, 4.3m, 4.0m, 48, 0), (4.5m, 4.4m, 4.7m, 32, null)],
        ["EST-004"] = [(3.6m, 3.9m, 4.1m, 48, 0), (3.4m, 3.0m, 3.5m, 48, 2), (4.0m, 4.2m, 3.9m, 64, 3), (4.5m, 4.6m, 4.4m, 32, 1)],
        ["EST-005"] = [(3.2m, 3.0m, 3.3m, 48, 4), (1.9m, 1.6m, 1.9m, 48, 5), (2.4m, 2.2m, 2.3m, 64, 6), (3.8m, 3.5m, 3.9m, 32, 2)],
        ["EST-006"] = [(4.1m, 4.0m, 4.3m, 48, 1), (3.6m, 3.8m, 3.9m, 48, 0), (3.9m, 4.1m, null, 64, 2), (4.2m, 4.0m, 4.4m, 32, 0)],
        // Estudiante extra para RES-12.a (datos alternativos; Contabilidad: 0.3*5.0 + 0.3*4.0 + 0.4*4.5 = 4.5).
        ["EST-007"] = [(5.0m, 4.0m, 4.5m, 48, 1), (3.0m, 3.0m, 3.0m, 48, 0), (3.0m, 3.0m, 3.0m, 64, 0), (3.0m, 3.0m, 3.0m, 32, 0)],
    };

    public static readonly EstudianteFuente[] Estudiantes =
    [
        new("EST-001", "Laura Gómez", "ING-SIS", 3), new("EST-002", "Andrés Martínez", "ING-SIS", 3),
        new("EST-003", "Sofía Herrera", "ING-SIS", 3), new("EST-004", "Camilo Rojas", "ADM", 2),
        new("EST-005", "Valentina Castro", "ADM", 2), new("EST-006", "Mateo Vargas", "ADM", 2),
    ];

    public static readonly EstudianteFuente EstudianteExtra = new("EST-007", "Daniela Prueba", "ADM", 2);

    public static IReadOnlyList<NotaFuente> Notas(string est, string programa) => Catalogo(programa).Select((c, i) =>
    {
        var f = Filas[est][i];
        return new NotaFuente(est, c.Cod, c.Nombre, c.Creditos, f.Item1, f.Item2, f.Item3);
    }).ToList();

    public static IReadOnlyList<AsistenciaFuente> Asistencia(string est, string programa) => Catalogo(programa).Select((c, i) =>
    {
        var f = Filas[est][i];
        return new AsistenciaFuente(est, c.Cod, c.Nombre, f.Item4, f.Item5);
    }).ToList();

    private static (string Cod, string Nombre, int Creditos)[] Catalogo(string programa) => programa == "ING-SIS" ? Ing : Adm;

    public static RosterSnapshot Roster() => new(
        Estudiantes.Select(e => new RosterEstudiante(e.Id, SinTildes(e.Nombre.ToLowerInvariant()), e.ProgramaId)).ToList(),
        [new RosterCoordinador("COORD-01"), new RosterCoordinador("COORD-02")],
        [new RosterPrograma("ING-SIS", "ingenieria de sistemas"), new RosterPrograma("ADM", "administracion de empresas")],
        DateTimeOffset.UnixEpoch);

    private static string SinTildes(string s) =>
        s.Replace("á", "a", StringComparison.Ordinal).Replace("é", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal).Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal);
}

/// <summary>Fuente de datos espia con los datos del material: registra cada lectura (sin-consulta) y permite agregar estudiantes.</summary>
internal sealed class FuenteMaterial : IAcademicDataSource
{
    private readonly ConcurrentQueue<string> _lecturas = new();

    public List<EstudianteFuente> Estudiantes { get; } = DatosMaterial.Estudiantes.ToList();

    public Dictionary<string, CoordinadorFuente> Coordinadores { get; } = new(StringComparer.Ordinal)
    {
        ["COORD-01"] = new("COORD-01", "Marcela Ríos", ["ING-SIS"]),
        ["COORD-02"] = new("COORD-02", "Coordinador Dos", ["ADM"]),
    };

    public Func<string, Exception?>? Fallo { get; set; }

    public IReadOnlyList<string> Lecturas => _lecturas.ToArray();

    /// <summary>Lecturas academicas (notas, asistencia, listados): excluye la validacion de identidad y las cargas del roster.</summary>
    public IReadOnlyList<string> LecturasAcademicas =>
        _lecturas.Where(l => l.StartsWith("notas:", StringComparison.Ordinal)
            || l.StartsWith("asistencia:", StringComparison.Ordinal)
            || l.StartsWith("programa:", StringComparison.Ordinal)).ToArray();

    public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("estudiante:" + estudianteId);
        return Task.FromResult(Estudiantes.FirstOrDefault(e => e.Id == estudianteId));
    }

    public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("notas:" + estudianteId);
        var e = Estudiantes.First(x => x.Id == estudianteId);
        return Task.FromResult(DatosMaterial.Notas(estudianteId, e.ProgramaId));
    }

    public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("asistencia:" + estudianteId);
        var e = Estudiantes.First(x => x.Id == estudianteId);
        return Task.FromResult(DatosMaterial.Asistencia(estudianteId, e.ProgramaId));
    }

    public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct)
    {
        Registrar("coordinador:" + coordinadorId);
        return Task.FromResult(Coordinadores.GetValueOrDefault(coordinadorId));
    }

    public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct)
    {
        Registrar("roster-programas");
        return Task.FromResult<IReadOnlyList<ProgramaFuente>>(
        [
            new ProgramaFuente("ING-SIS", "Ingeniería de Sistemas", "COORD-01"),
            new ProgramaFuente("ADM", "Administración de Empresas", "COORD-02"),
        ]);
    }

    public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct)
    {
        Registrar(CargaDeRoster ? "roster-estudiantes:" + programaId : "programa:" + programaId);
        return Task.FromResult<IReadOnlyList<EstudianteFuente>>(Estudiantes.Where(e => e.ProgramaId == programaId).ToList());
    }

    /// <summary>Mientras es true, los listados se registran como carga del roster (no son consultas academicas).</summary>
    public bool CargaDeRoster { get; set; }

    private void Registrar(string lectura)
    {
        _lecturas.Enqueue(lectura);
        var ex = Fallo?.Invoke(lectura);
        if (ex is not null)
        {
            throw ex;
        }
    }
}

/// <summary>
/// LLM guionizado (solo tests, A-11): cada llamada consume el siguiente paso; el ultimo se repite. Captura las
/// solicitudes para inspeccionar el prompt. No hay red ni llave.
/// </summary>
internal sealed class LlmGuion(params Func<LlmRequest, LlmResponse>[] pasos) : ILlmClient
{
    private int _n;

    public ConcurrentQueue<LlmRequest> Solicitudes { get; } = new();

    public int Invocaciones => Solicitudes.Count;

    public async Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct)
    {
        Solicitudes.Enqueue(solicitud);
        var i = Math.Min(Interlocked.Increment(ref _n) - 1, pasos.Length - 1);
        await Task.Yield();
        return pasos[i](solicitud);
    }

    public static JsonElement Args(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>Paso que propone invocar herramientas (una o varias en la misma ronda).</summary>
    public static Func<LlmRequest, LlmResponse> Invoca(int entrada, int salida, params (string Nombre, string ArgsJson)[] invocaciones) =>
        _ => new LlmResponse(
            string.Empty,
            invocaciones.Select((x, i) => new InvocacionPropuesta($"call-{i + 1}", x.Nombre, Args(x.ArgsJson))).ToList(),
            new UsoTokens(entrada, salida));

    public static Func<LlmRequest, LlmResponse> Invoca(string nombre, string argsJson = "{}") => Invoca(100, 20, (nombre, argsJson));

    /// <summary>Paso final: el JSON estructurado de SPEC 7.4.</summary>
    public static Func<LlmRequest, LlmResponse> Final(string respuesta, string[]? chunkIds = null, int entrada = 200, int salida = 30) =>
        _ => new LlmResponse(
            JsonSerializer.Serialize(new { respuesta, chunkIdsCitados = chunkIds ?? [] }),
            [],
            new UsoTokens(entrada, salida));

    public static Func<LlmRequest, LlmResponse> Falla(Exception ex) => _ => throw ex;

    /// <summary>Todo lo que el LLM llega a ver en una solicitud (sistema, mensajes y herramientas).</summary>
    public static string Prompt(LlmRequest r) =>
        r.Sistema + "\n" + string.Join("\n", r.Mensajes.Select(m => m.Contenido)) + "\n" +
        string.Join("\n", r.Herramientas.Select(t => t.Nombre + t.Descripcion + t.EsquemaArgumentos.GetRawText()));
}

internal sealed class RelojFijoE2E : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 11, 25, 14, 3, 11, 512, TimeSpan.Zero);
}

/// <summary>Captura todo lo que cualquier logger escribe (mensaje formateado y excepcion) para buscar fugas.</summary>
internal sealed class LogsCapturados : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lineas = new();

    public string Texto => string.Join("\n", _lineas);

    public ILogger CreateLogger(string categoryName) => new Registro(categoryName, _lineas);

    public void Dispose()
    {
    }

    private sealed class Registro(string categoria, ConcurrentQueue<string> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            destino.Enqueue($"{categoria} {logLevel}: {formatter(state, exception)} {exception}");
    }
}

/// <summary>
/// Host en proceso del flujo completo: fuente con datos del material, roster en memoria (o real sobre la fuente),
/// auditoria JSON a un StringWriter (el "stdout") y LLM guionizado. Con <c>Llm == null</c> se usa el cliente REAL
/// configurado por AddTutorFlowIa (sin llave: debe responder 503).
/// </summary>
internal sealed class FabricaE2E : WebApplicationFactory<Program>
{
    public FuenteMaterial Fuente { get; } = new();

    public LlmGuion? Llm { get; init; }

    public StringWriter Stdout { get; } = new();

    public LogsCapturados Logs { get; } = new();

    public RosterFalso Roster { get; } = new() { Snapshot = DatosMaterial.Roster() };

    public Dictionary<string, string?> Ajustes { get; } = new()
    {
        ["KNOWLEDGE_PATH"] = RutaRepo.Reglamento,
        ["LLM_API_KEY"] = "test-key-NOT-REAL",
        ["RETRIEVAL_STRATEGY"] = "full-context",
    };

    /// <summary>Roster REAL (RosterProvider) cargado desde la fuente: sin ningun dato fijo en el codigo.</summary>
    public bool RosterDesdeFuente { get; init; }

    public Action<IServiceCollection>? Extra { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (k, v) in Ajustes)
        {
            if (v is not null)
            {
                builder.UseSetting(k, v);
            }
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAcademicDataSource>();
            services.RemoveAll<IRosterProvider>();
            services.RemoveAll<IAuditLog>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new RelojFijoE2E());
            services.AddSingleton<IAcademicDataSource>(Fuente);
            services.AddSingleton<IAuditLog>(sp => new JsonLineAuditLog(
                new AuditoriaStdout(Stdout), null, sp.GetRequiredService<ILoggerFactory>()));
            services.AddSingleton<ILoggerProvider>(Logs);
            if (RosterDesdeFuente)
            {
                services.AddSingleton(sp => new RosterProvider(
                    Fuente, sp.GetRequiredService<IAuditLog>(), sp.GetRequiredService<IClock>(), TimeSpan.FromMinutes(5)));
                services.AddSingleton<IRosterProvider>(sp => sp.GetRequiredService<RosterProvider>());
            }
            else
            {
                services.AddSingleton<IRosterProvider>(Roster);
            }

            if (Llm is not null)
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton<ILlmClient>(Llm);
            }

            Extra?.Invoke(services);
        });
    }

    /// <summary>Carga el roster real desde la fuente; esas lecturas no son consultas academicas (SPEC 6.6).</summary>
    public async Task CargarRosterAsync()
    {
        var roster = Services.GetRequiredService<RosterProvider>();
        Fuente.CargaDeRoster = true;
        try
        {
            Assert.True(await roster.CargarAsync(CancellationToken.None));
        }
        finally
        {
            Fuente.CargaDeRoster = false;
        }
    }

    /// <summary>Eventos <c>pregunta</c> escritos al stdout de auditoria (una linea JSON por evento).</summary>
    public IReadOnlyList<JsonElement> EventosPregunta() => Lineas()
        .Where(e => e.GetProperty("evento").GetString() == "pregunta")
        .ToList();

    public IReadOnlyList<JsonElement> Lineas() =>
        Stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l =>
            {
                using var d = JsonDocument.Parse(l);
                return d.RootElement.Clone();
            })
            .ToList();

    public static async Task<HttpResponseMessage> PreguntarAsync(
        HttpClient http, string rol, string id, string pregunta, string? requestId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/preguntar")
        {
            Content = FabricaApi.Json(Asercion.ConUsuario(Asercion.Usuario(rol, id), pregunta)),
        };
        if (requestId is not null)
        {
            req.Headers.Add("X-Request-Id", requestId);
        }

        return await http.SendAsync(req);
    }
}
