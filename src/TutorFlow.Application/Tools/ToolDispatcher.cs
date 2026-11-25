using TutorFlow.Application.Audit;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tools;

/// <summary>
/// Despachador unico de herramientas (SPEC 6.2 L3/L4, SPEC 8): herramienta expuesta al rol (L3) -> argumentos estrictos
/// -> <see cref="IAuthorizationPolicy"/> ANTES de cualquier lectura de la fuente -> ejecucion y citas deterministas.
/// El principal lo aporta el servidor; el LLM solo propone nombre y argumentos. Toda denegacion lanza
/// <see cref="AccesoDenegadoException"/> (motivo "herramienta" o "politica") y aborta la peticion.
/// </summary>
public sealed class ToolDispatcher : IToolDispatcher
{
    private const string MotivoHerramienta = "herramienta";
    private const string MotivoPolitica = "politica";

    private readonly IAcademicDataSource _fuente;
    private readonly IRosterProvider _roster;
    private readonly IAuthorizationPolicy _politica;

    private readonly IContextoAuditoria? _auditoria;

    public ToolDispatcher(IAcademicDataSource fuente, IRosterProvider roster, IAuthorizationPolicy politica, IContextoAuditoria? auditoria = null)
    {
        ArgumentNullException.ThrowIfNull(fuente);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(politica);
        _fuente = fuente;
        _roster = roster;
        _politica = politica;
        _auditoria = auditoria;
    }

    public IReadOnlyList<ToolDefinition> HerramientasExpuestas(PrincipalAcademico principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ToolCatalog.Definiciones(principal.Rol);
    }

    public async Task<ResultadoHerramienta> DespacharAsync(
        PrincipalAcademico principal,
        InvocacionPropuesta invocacion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(invocacion);

        // Gancho de auditoria (SPEC 9.2): objetivo intentado, decision y, solo en exito, la consulta ejecutada.
        string? objetivoIntentado = null;
        bool? esPrograma = null;
        try
        {
            var resultado = await DespacharCoreAsync(principal, invocacion, o => (objetivoIntentado, esPrograma) = o, ct).ConfigureAwait(false);
            if (resultado.Objetivo is not null)
            {
                _auditoria?.ConsultaExitosa(invocacion.Nombre, resultado.Objetivo, esPrograma ?? false);
            }

            return resultado;
        }
        catch (AccesoDenegadoException ex)
        {
            _auditoria?.HerramientaDenegada(invocacion.Nombre, objetivoIntentado);
            _auditoria?.Denegar(ex.Motivo);
            throw;
        }
    }

    private async Task<ResultadoHerramienta> DespacharCoreAsync(
        PrincipalAcademico principal,
        InvocacionPropuesta invocacion,
        Action<(string? Objetivo, bool? EsPrograma)> intento,
        CancellationToken ct)
    {
        // L3: inexistente o no expuesta a este rol.
        var spec = ToolCatalog.Buscar(invocacion.Nombre);
        if (spec is null || !ToolCatalog.Expuesta(spec, principal.Rol))
        {
            throw Denegar(principal, MotivoHerramienta);
        }

        // Esquema estricto: no llega a la fuente ni a la politica.
        var args = ArgumentValidator.Validar(spec, invocacion.Argumentos) ?? throw Denegar(principal, MotivoHerramienta);
        intento(spec.Individual
            ? (args.EstudianteId ?? (principal.Rol == RolAcademico.Estudiante ? principal.Id : null), false)
            : (args.ProgramaId ?? (principal.ProgramasCoordinados.Count > 0 ? string.Join(",", principal.ProgramasCoordinados) : null), true));

        return spec.Individual
            ? await EjecutarIndividualAsync(principal, spec, args, ct).ConfigureAwait(false)
            : await EjecutarProgramaAsync(principal, spec, args, ct).ConfigureAwait(false);
    }

    private async Task<ResultadoHerramienta> EjecutarIndividualAsync(
        PrincipalAcademico principal,
        ToolSpec spec,
        ArgumentosHerramienta args,
        CancellationToken ct)
    {
        // El estudiante que omite estudianteId usa el suyo; el coordinador debe indicarlo (SPEC 8).
        var objetivo = args.EstudianteId ?? (principal.Rol == RolAcademico.Estudiante ? principal.Id : null);
        if (objetivo is null)
        {
            throw Denegar(principal, MotivoHerramienta);
        }

        // programa(X) sale del roster, nunca de la pregunta ni del LLM; si X no esta se deniega antes de leer.
        var roster = await _roster.ObtenerAsync(ct).ConfigureAwait(false);
        var programa = roster.Estudiantes.FirstOrDefault(e => string.Equals(e.Id, objetivo, StringComparison.Ordinal))?.ProgramaId;
        if (programa is null)
        {
            // SPEC 6.1: un objetivo ausente del roster se deniega, tambien si es el propio estudiante.
            throw Denegar(principal, MotivoPolitica);
        }

        // L4: autorizar ANTES de cualquier lectura.
        var decision = _politica.Authorize(principal, new RecursoAcademico(TipoRecurso.DatosEstudiante, objetivo, programa));
        if (!decision.Permitido)
        {
            throw Denegar(principal, MotivoPolitica);
        }

        var citas = new CitasHerramienta();
        string contenido;
        if (spec.Tipo == ToolKind.Asistencia)
        {
            var asistencia = await _fuente.ObtenerAsistenciaAsync(objetivo, ct).ConfigureAwait(false);
            var periodo = PeriodoAcademico.Construir(Notas(asistencia), asistencia);
            contenido = ToolRenderers.Asistencia(objetivo, periodo, args.Asignatura, citas);
        }
        else
        {
            var notas = await _fuente.ObtenerNotasAsync(objetivo, ct).ConfigureAwait(false);
            var asistencia = await _fuente.ObtenerAsistenciaAsync(objetivo, ct).ConfigureAwait(false);
            var periodo = PeriodoAcademico.Construir(notas, asistencia);
            contenido = spec.Tipo switch
            {
                ToolKind.Notas => ToolRenderers.Notas(objetivo, periodo, citas),
                ToolKind.Resumen => ToolRenderers.Resumen(objetivo, periodo, citas),
                ToolKind.Beca => ToolRenderers.Beca(objetivo, periodo, citas),
                _ => throw Denegar(principal, MotivoHerramienta),
            };
        }

        return new ResultadoHerramienta(contenido, citas.Construir(), UsoDatosEstudiante: true, objetivo);
    }

    private async Task<ResultadoHerramienta> EjecutarProgramaAsync(
        PrincipalAcademico principal,
        ToolSpec spec,
        ArgumentosHerramienta args,
        CancellationToken ct)
    {
        IReadOnlyList<string> programas = args.ProgramaId is { } p ? [p] : principal.ProgramasCoordinados;
        if (programas.Count == 0)
        {
            throw Denegar(principal, MotivoPolitica);
        }

        // L4: todos los programas se autorizan antes de leer cualquiera.
        foreach (var programa in programas)
        {
            var decision = _politica.Authorize(principal, new RecursoAcademico(TipoRecurso.ProgramaEstudiantes, null, programa));
            if (!decision.Permitido)
            {
                throw Denegar(principal, MotivoPolitica);
            }
        }

        var citas = new CitasHerramienta();
        var porPrograma = new List<(string Programa, IReadOnlyList<EstudianteFuente> Estudiantes)>();
        foreach (var programa in programas)
        {
            var estudiantes = await _fuente.ListarEstudiantesProgramaAsync(programa, ct).ConfigureAwait(false);
            porPrograma.Add((programa, estudiantes.OrderBy(e => e.Id, StringComparer.Ordinal).ToList()));
            citas.DatosPrograma(programa);
        }

        string contenido;
        if (spec.Tipo == ToolKind.ListarPrograma)
        {
            contenido = PeriodoAcademico.Json(w =>
            {
                w.WriteStartObject();
                w.WriteStartArray("programas");
                foreach (var (programa, estudiantes) in porPrograma)
                {
                    w.WriteStartObject();
                    w.WriteString("programaId", programa);
                    w.WriteStartArray("estudiantes");
                    foreach (var e in estudiantes)
                    {
                        w.WriteStartObject();
                        w.WriteString("id", e.Id);
                        w.WriteString("nombre", e.Nombre);
                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            });
        }
        else
        {
            contenido = await PerdidasAsync(porPrograma, citas, ct).ConfigureAwait(false);
        }

        // El puerto solo admite un objetivo: varios programas se separan con coma (programasConsultados, SPEC 9.2).
        return new ResultadoHerramienta(contenido, citas.Construir(), UsoDatosEstudiante: true, string.Join(",", programas));
    }

    private async Task<string> PerdidasAsync(
        List<(string Programa, IReadOnlyList<EstudianteFuente> Estudiantes)> porPrograma,
        CitasHerramienta citas,
        CancellationToken ct)
    {
        citas.Definitiva().AprobadaOPerdida();
        var salida = new List<(string Programa, List<(EstudianteFuente Est, List<AsignaturaConNombre> Perdidas)> Con)>();
        foreach (var (programa, estudiantes) in porPrograma)
        {
            var con = new List<(EstudianteFuente, List<AsignaturaConNombre>)>();
            foreach (var e in estudiantes)
            {
                var notas = await _fuente.ObtenerNotasAsync(e.Id, ct).ConfigureAwait(false);
                var asistencia = await _fuente.ObtenerAsistenciaAsync(e.Id, ct).ConfigureAwait(false);
                var periodo = PeriodoAcademico.Construir(notas, asistencia);
                var perdidas = periodo.Where(p => p.Dominio.Perdida).ToList();
                if (perdidas.Count > 0)
                {
                    con.Add((e, perdidas));
                    if (perdidas.Any(p => p.Dominio.PierdePorInasistencia == true))
                    {
                        citas.PerdidaPorInasistencia();
                    }
                }
            }

            salida.Add((programa, con));
        }

        return PeriodoAcademico.Json(w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("programas");
            foreach (var (programa, con) in salida)
            {
                w.WriteStartObject();
                w.WriteString("programaId", programa);
                w.WriteStartArray("estudiantes");
                foreach (var (est, perdidas) in con)
                {
                    w.WriteStartObject();
                    w.WriteString("id", est.Id);
                    w.WriteString("nombre", est.Nombre);
                    w.WriteStartArray("asignaturasPerdidas");
                    foreach (var (a, nombre) in perdidas)
                    {
                        ToolRenderers.EscribirPerdida(w, a, nombre);
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    /// <summary>La herramienta de asistencia solo lee asistencia; el nombre de la asignatura viene en esa misma fila.</summary>
    private static IReadOnlyList<NotaFuente> Notas(IReadOnlyList<AsistenciaFuente> asistencia) =>
        asistencia.Select(a => new NotaFuente(a.EstudianteId, a.Codigo, a.Asignatura, 1, null, null, null)).ToList();

    private static AccesoDenegadoException Denegar(PrincipalAcademico principal, string motivo) => new(principal.Rol, motivo);
}
