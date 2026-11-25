using System.Net;
using System.Text;
using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Tests.Datos;

internal sealed class RelojFalso : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class AuditoriaFalsa : IAuditLog
{
    public List<EventoRosterCargado> Roster { get; } = [];

    public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct) => Task.CompletedTask;

    public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct)
    {
        Roster.Add(evento);
        return Task.CompletedTask;
    }
}

/// <summary>Manejador HTTP guionizado: cada llamada consume el comportamiento que decida la funcion.</summary>
internal sealed class ManejadorFalso : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;

    public ManejadorFalso(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public List<string> Rutas { get; } = [];

    public int Llamadas => Rutas.Count;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Rutas.Add(request.RequestUri!.AbsoluteUri);
        return _responder(request, Rutas.Count, cancellationToken);
    }

    public static Task<HttpResponseMessage> Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    public static Task<HttpResponseMessage> Estado(HttpStatusCode status) => Task.FromResult(new HttpResponseMessage(status));
}
