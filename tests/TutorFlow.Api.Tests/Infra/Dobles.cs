using System.Collections.Concurrent;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;

namespace TutorFlow.Api.Tests.Infra;

/// <summary>Fuente de datos falsa y espia: registra cada llamada y puede fallar. Datos sinteticos de prueba.</summary>
internal sealed class FuenteEspia : IAcademicDataSource
{
    private readonly ConcurrentQueue<string> _llamadas = new();

    public Dictionary<string, EstudianteFuente> Estudiantes { get; } = new(StringComparer.Ordinal)
    {
        ["EST-001"] = new("EST-001", "Estudiante Uno", "ING-SIS", 5),
        ["EST-002"] = new("EST-002", "Estudiante Dos", "ING-SIS", 5),
    };

    public Dictionary<string, CoordinadorFuente> Coordinadores { get; } = new(StringComparer.Ordinal)
    {
        ["COORD-01"] = new("COORD-01", "Coordinador Uno", ["ING-SIS"]),
        ["COORD-02"] = new("COORD-02", "Coordinador Dos", ["ADM"]),
    };

    public Func<string, Exception?>? Fallo { get; set; }

    public IReadOnlyList<string> Llamadas => _llamadas.ToArray();

    public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("GET estudiante " + estudianteId);
        return Task.FromResult(Estudiantes.GetValueOrDefault(estudianteId));
    }

    public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct)
    {
        Registrar("GET coordinador " + coordinadorId);
        return Task.FromResult(Coordinadores.GetValueOrDefault(coordinadorId));
    }

    public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("GET notas " + estudianteId);
        return Task.FromResult<IReadOnlyList<NotaFuente>>([]);
    }

    public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct)
    {
        Registrar("GET asistencia " + estudianteId);
        return Task.FromResult<IReadOnlyList<AsistenciaFuente>>([]);
    }

    public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct)
    {
        Registrar("GET programas");
        return Task.FromResult<IReadOnlyList<ProgramaFuente>>([]);
    }

    public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct)
    {
        Registrar("GET programa estudiantes " + programaId);
        return Task.FromResult<IReadOnlyList<EstudianteFuente>>([]);
    }

    private void Registrar(string llamada)
    {
        _llamadas.Enqueue(llamada);
        var ex = Fallo?.Invoke(llamada);
        if (ex is not null)
        {
            throw ex;
        }
    }
}

/// <summary>Roster en memoria: vigente o caido (DependenciaNoDisponibleException).</summary>
internal sealed class RosterFalso : IRosterProvider
{
    public bool Caido { get; set; }

    public RosterSnapshot Snapshot { get; set; } = new([], [], [], DateTimeOffset.UnixEpoch);

    public Task<RosterSnapshot> ObtenerAsync(CancellationToken ct)
    {
        if (Caido)
        {
            throw new DependenciaNoDisponibleException("roster caido");
        }

        return Task.FromResult(Snapshot);
    }
}

/// <summary>Auditoria que acumula en memoria los eventos.</summary>
internal sealed class AuditoriaMemoria : IAuditLog
{
    public ConcurrentQueue<EventoRosterCargado> Roster { get; } = new();

    public ConcurrentQueue<EventoPregunta> Preguntas { get; } = new();

    public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct)
    {
        Preguntas.Enqueue(evento);
        return Task.CompletedTask;
    }

    public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct)
    {
        Roster.Enqueue(evento);
        return Task.CompletedTask;
    }
}

/// <summary>Cliente LLM espia: cuenta las invocaciones (L2 debe rechazar ANTES de llamarlo).</summary>
internal sealed class LlmEspia : ILlmClient
{
    private int _invocaciones;

    public int Invocaciones => Volatile.Read(ref _invocaciones);

    public Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct)
    {
        Interlocked.Increment(ref _invocaciones);
        return Task.FromResult(new LlmResponse("(llm espia)", [], new UsoTokens(0, 0)));
    }
}
