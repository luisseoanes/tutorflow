using TutorFlow.Api.Contracts;
using TutorFlow.Api.Http;
using TutorFlow.Application.Preguntar;

namespace TutorFlow.Api;

public static class Endpoints
{
    public static void MapTutorFlow(this IEndpointRouteBuilder app)
    {
        // Liveness: no depende de la fuente de datos, el roster ni el LLM (SPEC 3.2).
        app.MapGet("/api/salud", (HttpContext ctx) =>
            Respuestas.EscribirJsonAsync(ctx.Response, StatusCodes.Status200OK, new SaludResponse("ok")));

        app.MapPost("/api/asistente/preguntar", async (HttpContext ctx, IPreguntarAsistente caso) =>
        {
            var requestId = RequestIdMiddleware.Obtener(ctx);
            var lectura = await PreguntarBodyReader.LeerAsync(ctx.Request, requestId, ctx.RequestAborted);
            if (lectura.Comando is null)
            {
                await Respuestas.EscribirProblemaAsync(
                    ctx.Response, StatusCodes.Status400BadRequest, "Solicitud invalida", lectura.Detalle!, requestId);
                return;
            }

            var resultado = await caso.EjecutarAsync(lectura.Comando, ctx.RequestAborted);
            var respuesta = new PreguntarResponse(
                resultado.Respuesta,
                resultado.Citas.Select(c => new CitaDto(c.Documento, c.Seccion)).ToList(),
                resultado.UsoDatosEstudiante,
                resultado.Tokens,
                Math.Round(resultado.CostoUsd, 6, MidpointRounding.AwayFromZero));
            await Respuestas.EscribirJsonAsync(ctx.Response, StatusCodes.Status200OK, respuesta);
        });
    }
}
