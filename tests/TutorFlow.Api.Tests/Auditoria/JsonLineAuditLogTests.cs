using System.Text.Json;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Audit;

namespace TutorFlow.Api.Tests.Auditoria;

public class JsonLineAuditLogTests
{
    private sealed class Captura : ILoggerProvider
    {
        public List<(string Categoria, string Mensaje)> Lineas { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Registro(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Registro(Captura padre, string categoria) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                padre.Lineas.Add((categoria, formatter(state, exception)));
        }
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.3")]
    public async Task Roster_cargado_se_escribe_como_una_linea_JSON_en_TutorFlow_Audit_sin_ids_ni_nombres()
    {
        var captura = new Captura();
        using var fabrica = LoggerFactory.Create(b => b.AddProvider(captura));
        var audit = new JsonLineAuditLog(fabrica);

        await audit.RegistrarRosterCargadoAsync(
            new EventoRosterCargado(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), 12, 3, "ok"),
            CancellationToken.None);

        var linea = Assert.Single(captura.Lineas);
        Assert.Equal("TutorFlow.Audit", linea.Categoria);
        Assert.DoesNotContain('\n', linea.Mensaje);
        using var doc = JsonDocument.Parse(linea.Mensaje);
        Assert.Equal("roster.cargado", doc.RootElement.GetProperty("evento").GetString());
        Assert.Equal(12, doc.RootElement.GetProperty("estudiantes").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("programas").GetInt32());
        Assert.Equal("ok", doc.RootElement.GetProperty("resultado").GetString());
        Assert.Equal(
            ["evento", "ts", "estudiantes", "programas", "resultado"],
            doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
    }
}
