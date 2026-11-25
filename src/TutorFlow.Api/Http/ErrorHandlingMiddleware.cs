using TutorFlow.Application.Audit;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Llm;
using TutorFlow.Domain.Academico;

namespace TutorFlow.Api.Http;

/// <summary>
/// Mapeo central de errores tipados a respuestas (SPEC 3.3, AGENTS 5). No se traga ningun error: cada rama
/// responde y deja un log sin datos academicos; el 500 no filtra detalles internos.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception) when (context.Response.HasStarted)
        {
            throw;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cancelo: no hay a quien responder.
        }
        catch (IdentidadInvalidaException ex)
        {
            _logger.LogInformation("Identidad rechazada por L1 (motivo: {Motivo}).", ex.Motivo);
            context.RequestServices.GetService<IContextoAuditoria>()?.Denegar("identidad");
            await Responder403(context, Respuestas.Identidad());
        }
        catch (AccesoDenegadoException ex)
        {
            _logger.LogInformation("Acceso denegado (motivo: {Motivo}).", ex.Motivo);
            context.RequestServices.GetService<IContextoAuditoria>()?.Denegar(ex.Motivo);
            await Responder403(context, Respuestas.Acceso(ex.Rol, ex.Tokens, ex.CostoUsd));
        }
        catch (DependenciaNoDisponibleException)
        {
            _logger.LogWarning("Dependencia no disponible.");
            await Problema(context, 503, "Servicio no disponible", "Una dependencia necesaria no esta disponible. Intenta de nuevo mas tarde.");
        }
        catch (DatoAcademicoInvalidoException)
        {
            _logger.LogError("La fuente de datos devolvio un dato invalido.");
            await Problema(context, 503, "Servicio no disponible", "Una dependencia necesaria devolvio datos invalidos. Intenta de nuevo mas tarde.");
        }
        catch (LlmNoDisponibleException ex)
        {
            // Solo el codigo HTTP del proveedor (si lo hay): jamas el mensaje, la llave ni el cuerpo del proveedor.
            _logger.LogWarning("LLM no disponible (estado del proveedor: {EstadoProveedor}).", ex.StatusCode);
            await Problema(context, 503, "Servicio no disponible", "El modelo de lenguaje no esta disponible. Intenta de nuevo mas tarde.");
        }
        catch (Exception ex)
        {
            // Se registra el tipo para operacion; el cliente solo recibe el requestId.
            _logger.LogError("Error no controlado ({TipoError}).", ex.GetType().Name);
            await Problema(context, 500, "Error interno", "Ocurrio un error interno. Reporta el identificador de la peticion.");
        }
    }

    private static Task Responder403(HttpContext context, Contracts.PreguntarResponse cuerpo)
    {
        context.Response.Clear();
        context.Response.Headers[RequestIdMiddleware.HeaderName] = RequestIdMiddleware.Obtener(context);
        return Respuestas.EscribirJsonAsync(context.Response, StatusCodes.Status403Forbidden, cuerpo);
    }

    private static Task Problema(HttpContext context, int status, string titulo, string detalle)
    {
        context.Response.Clear();
        var id = RequestIdMiddleware.Obtener(context);
        context.Response.Headers[RequestIdMiddleware.HeaderName] = id;
        return Respuestas.EscribirProblemaAsync(context.Response, status, titulo, detalle, id);
    }
}
