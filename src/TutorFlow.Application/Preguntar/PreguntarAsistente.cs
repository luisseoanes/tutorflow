using TutorFlow.Application.Audit;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Security;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Preguntar;

/// <summary>Entrada ya validada en forma (SPEC 3.3 reglas 1-4). <c>Rol</c> e <c>Id</c> son null si faltan o no son string.</summary>
public sealed record PreguntarComando(string Pregunta, string? Rol, string? Id, string RequestId);

public sealed record PreguntarResultado(
    string Respuesta,
    IReadOnlyList<CitaDocumental> Citas,
    bool UsoDatosEstudiante,
    int Tokens,
    decimal CostoUsd);

/// <summary>Tiempo total por peticion (SPEC 10.1: 30 s). Vencido, la peticion responde 503.</summary>
public sealed record PreguntarOpciones(TimeSpan TimeoutTotal)
{
    public static readonly TimeSpan TimeoutPorDefecto = TimeSpan.FromSeconds(30);

    public static PreguntarOpciones PorDefecto { get; } = new(TimeoutPorDefecto);
}

/// <summary>Estrategia de recuperacion y modelo configurados: solo se registran en la auditoria (SPEC 9.2).</summary>
public sealed record InfoFlujoLlm(string Estrategia, string Modelo);

public interface IPreguntarAsistente
{
    Task<PreguntarResultado> EjecutarAsync(PreguntarComando comando, CancellationToken ct);
}

/// <summary>
/// Caso de uso de SPEC 12.3: L1 (403-identidad / 503) -> roster vigente (503) -> L2a + L2b (403-acceso, antes de cualquier
/// recuperacion o LLM) -> recuperacion (UNA pasada) -> generador (bucle LLM + <see cref="IToolDispatcher"/> de la
/// peticion: L3/L4) -> L5 (403-acceso "salida", conservando tokens y costo) -> resultado del contrato.
/// Ninguna decision de permisos sale del LLM ni del texto de la pregunta mas alla de las guardias deterministas.
/// El retriever y el generador se resuelven de forma perezosa: un fallo de su configuracion no debe tumbar los 400 ni
/// los 403 de L1/L2 (SPEC 10.2).
/// </summary>
public sealed class PreguntarAsistente : IPreguntarAsistente
{
    private readonly IValidadorIdentidad _identidad;
    private readonly IRosterProvider _roster;
    private readonly Func<IRetriever> _retriever;
    private readonly Func<IGeneradorRespuesta> _generador;
    private readonly IToolDispatcher _herramientas;
    private readonly PreguntarOpciones _opciones;
    private readonly InfoFlujoLlm _info;
    private readonly IContextoAuditoria? _auditoria;

    public PreguntarAsistente(
        IValidadorIdentidad identidad,
        IRosterProvider roster,
        Func<IRetriever> retriever,
        Func<IGeneradorRespuesta> generador,
        IToolDispatcher herramientas,
        PreguntarOpciones opciones,
        InfoFlujoLlm info,
        IContextoAuditoria? auditoria = null)
    {
        ArgumentNullException.ThrowIfNull(identidad);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(generador);
        ArgumentNullException.ThrowIfNull(herramientas);
        ArgumentNullException.ThrowIfNull(opciones);
        ArgumentNullException.ThrowIfNull(info);
        _identidad = identidad;
        _roster = roster;
        _retriever = retriever;
        _generador = generador;
        _herramientas = herramientas;
        _opciones = opciones;
        _info = info;
        _auditoria = auditoria;
    }

    public async Task<PreguntarResultado> EjecutarAsync(PreguntarComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // Timeout TOTAL de la peticion (SPEC 10.1), enlazado a la cancelacion del cliente: si vence el nuestro -> 503.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_opciones.TimeoutTotal);
        try
        {
            return await EjecutarDentroDelLimiteAsync(comando, limite.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && limite.IsCancellationRequested)
        {
            throw new DependenciaNoDisponibleException("Se agoto el tiempo total de la peticion.");
        }
    }

    private async Task<PreguntarResultado> EjecutarDentroDelLimiteAsync(PreguntarComando comando, CancellationToken ct)
    {
        _auditoria?.Iniciar(comando.Pregunta, comando.Rol, comando.Id);
        var principal = await _identidad.ValidarAsync(comando.Rol, comando.Id, ct).ConfigureAwait(false);
        _auditoria?.IdentidadValidada();
        var roster = await _roster.ObtenerAsync(ct);

        // L2a + L2b: 403-acceso (motivo "referencia") antes de cualquier recuperacion o llamada al LLM.
        DenegarSiMarcada(GuardiaReferenciasExplicitas.Evaluar(comando.Pregunta, principal, roster), principal);
        DenegarSiMarcada(GuardiaSolicitudesTerceros.Evaluar(comando.Pregunta, principal, roster), principal);

        // Recuperacion: una sola pasada, sin abstencion previa al LLM (SPEC 7.3).
        var fragmentos = await _retriever().RecuperarAsync(comando.Pregunta, ct).ConfigureAwait(false);

        // Bucle LLM + herramientas. Toda AccesoDenegadoException del despachador (L3/L4) se propaga y aborta la peticion;
        // LlmNoDisponibleException la mapea el middleware a 503.
        _auditoria?.FlujoLlm(estrategia: _info.Estrategia, modelo: _info.Modelo);
        var registro = new RegistroCodigosAsignatura(_herramientas);
        RespuestaGenerada generada;
        try
        {
            generada = await _generador().GenerarAsync(principal, comando.Pregunta, fragmentos, registro, ct).ConfigureAwait(false);
        }
        catch (AccesoDenegadoException ex) when (ex.Tokens > 0)
        {
            // 403 por L4 a mitad del bucle: la auditoria conserva lo consumido, igual que el cuerpo (SPEC 3.4 y 9).
            _auditoria?.FlujoLlm(tokens: ex.Tokens, costoUsd: ex.CostoUsd);
            throw;
        }


        // L5: la respuesta no puede nombrar ni identificar a estudiantes fuera del alcance. Conserva lo consumido.
        var salida = GuardiaSalida.Evaluar(generada.Respuesta, principal, roster, registro.Codigos);
        if (salida.Marcada)
        {
            _auditoria?.ObjetivosDenegados(salida.Objetivos);
            _auditoria?.FlujoLlm(tokens: generada.Tokens, costoUsd: generada.CostoUsd);
            _auditoria?.Denegar(GuardiaSalida.Motivo);
            throw new AccesoDenegadoException(principal.Rol, GuardiaSalida.Motivo, generada.Tokens, generada.CostoUsd);
        }

        _auditoria?.FlujoLlm(
            citas: generada.Citas.Select(c => $"{c.Documento}#{c.Seccion}"),
            tokens: generada.Tokens,
            costoUsd: generada.CostoUsd);

        // RF-09: lo decide el codigo, no el LLM: 200 con al menos una consulta academica ejecutada y sin abstencion.
        var usoDatos = !generada.Abstuvo && generada.UsoDatosEstudiante;
        return new PreguntarResultado(generada.Respuesta, generada.Citas, usoDatos, generada.Tokens, generada.CostoUsd);
    }

    private void DenegarSiMarcada(ResultadoGuardia resultado, PrincipalAcademico principal)
    {
        if (resultado.Marcada)
        {
            _auditoria?.ObjetivosDenegados(resultado.Objetivos);
            _auditoria?.Denegar("referencia");
            throw new AccesoDenegadoException(principal.Rol, "referencia");
        }
    }
}
