using System.Diagnostics;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Ports;

namespace TutorFlow.Api.Http;

/// <summary>
/// Emite EXACTAMENTE UN evento <c>pregunta</c> (SPEC 9.1) por cada <c>POST /api/asistente/preguntar</c>, sea cual sea el
/// resultado (200, 400, 403, 5xx). Va por fuera del middleware de errores: el estado ya es el definitivo cuando la
/// respuesta empieza (<c>OnStarting</c>); si nunca empieza (cancelacion, excepcion), se emite al terminar. Un fallo al
/// auditar jamas altera la respuesta.
/// </summary>
public sealed class AuditoriaMiddleware
{
    public const string RutaPreguntar = "/api/asistente/preguntar";
    private const int EstadoClienteCancelo = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditoriaMiddleware> _logger;

    public AuditoriaMiddleware(RequestDelegate next, ILogger<AuditoriaMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !string.Equals(context.Request.Path.Value?.TrimEnd('/'), RutaPreguntar, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var cronometro = Stopwatch.StartNew();
        var servicios = context.RequestServices;
        var contexto = servicios.GetRequiredService<IContextoAuditoria>();
        var requestId = RequestIdMiddleware.Obtener(context);

        context.Response.OnStarting(() => EmitirAsync(context, contexto, requestId, cronometro, context.Response.StatusCode));
        try
        {
            await _next(context);
        }
        finally
        {
            var estado = context.Response.HasStarted
                ? context.Response.StatusCode
                : context.RequestAborted.IsCancellationRequested ? EstadoClienteCancelo : context.Response.StatusCode;
            await EmitirAsync(context, contexto, requestId, cronometro, estado);
        }
    }

    private async Task EmitirAsync(HttpContext context, IContextoAuditoria contexto, string requestId, Stopwatch cronometro, int estado)
    {
        try
        {
            var reloj = context.RequestServices.GetRequiredService<IClock>();
            var evento = contexto.ConstruirUnaVez(requestId, reloj.UtcNow, estado, cronometro.ElapsedMilliseconds);
            if (evento is not null)
            {
                await context.RequestServices.GetRequiredService<IAuditLog>().RegistrarPreguntaAsync(evento, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("No se pudo registrar el evento de auditoria ({TipoError}).", ex.GetType().Name);
        }
    }
}
