using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;

namespace TutorFlow.Infrastructure.Roster;

/// <summary>
/// Roster de sistema (SPEC 6.6): datos minimos, carga via GET /programas + GET /programas/{id}/estudiantes,
/// fail-closed ante ausencia o vencimiento (2 x TTL). No se atribuye a ninguna peticion de usuario.
/// </summary>
public sealed class RosterProvider : IRosterProvider
{
    private readonly IAcademicDataSource _fuente;
    private readonly IAuditLog _audit;
    private readonly IClock _clock;
    private readonly TimeSpan _ttl;
    private readonly ILogger<RosterProvider>? _logger;
    private volatile RosterSnapshot? _actual;

    public RosterProvider(IAcademicDataSource fuente, IAuditLog audit, IClock clock, TimeSpan ttl, ILogger<RosterProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fuente);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl));
        }

        _fuente = fuente;
        _audit = audit;
        _clock = clock;
        _ttl = ttl;
        _logger = logger;
    }

    public bool TieneSnapshot => _actual is not null;

    public Task<RosterSnapshot> ObtenerAsync(CancellationToken ct)
    {
        var snap = _actual;
        if (snap is null)
        {
            throw new DependenciaNoDisponibleException("El roster aun no se ha cargado.");
        }

        if (_clock.UtcNow - snap.CargadoEn > _ttl * 2)
        {
            throw new DependenciaNoDisponibleException("El roster supera 2 x TTL desde su ultima carga correcta.");
        }

        return Task.FromResult(snap);
    }

    /// <summary>Carga el roster. Devuelve true si tuvo exito; ante fallo conserva el snapshot anterior.</summary>
    public async Task<bool> CargarAsync(CancellationToken ct)
    {
        try
        {
            var programas = await _fuente.ListarProgramasAsync(ct);
            var estudiantes = new List<RosterEstudiante>();
            foreach (var p in programas)
            {
                var lista = await _fuente.ListarEstudiantesProgramaAsync(p.Id, ct);
                estudiantes.AddRange(lista.Select(e => new RosterEstudiante(e.Id, Normalizar(e.Nombre), e.ProgramaId)));
            }

            var coordinadores = programas
                .Where(p => !string.IsNullOrEmpty(p.CoordinadorId))
                .Select(p => p.CoordinadorId!)
                .Distinct(StringComparer.Ordinal)
                .Select(id => new RosterCoordinador(id))
                .ToList();
            var progs = programas.Select(p => new RosterPrograma(p.Id, Normalizar(p.Nombre))).ToList();

            var ahora = _clock.UtcNow;
            _actual = new RosterSnapshot(estudiantes, coordinadores, progs, ahora);
            await Auditar(new EventoRosterCargado(ahora, estudiantes.Count, progs.Count, "ok"), ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Fallo la carga del roster: {Tipo}", ex.GetType().Name);
            var previo = _actual;
            await Auditar(
                new EventoRosterCargado(_clock.UtcNow, previo?.Estudiantes.Count ?? 0, previo?.Programas.Count ?? 0, "error"),
                ct);
            return false;
        }
    }

    /// <summary>NFD sin diacriticos, minusculas, espacios colapsados.</summary>
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task Auditar(EventoRosterCargado evento, CancellationToken ct)
    {
        try
        {
            await _audit.RegistrarRosterCargadoAsync(evento, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning("No se pudo auditar roster.cargado: {Tipo}", ex.GetType().Name);
        }
    }
}
