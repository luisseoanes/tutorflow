using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TutorFlow.Application.Ports;

namespace TutorFlow.Api.Tests.Infra;

/// <summary>
/// Host en proceso con la fuente de datos y el roster reemplazados por dobles y sin el BackgroundService del roster
/// (no hay red ni carga real).
/// </summary>
internal sealed class FabricaApi : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _extra;

    public FabricaApi(Action<IServiceCollection>? extra = null)
    {
        _extra = extra;
        Fuente = new FuenteEspia();
        Roster = new RosterFalso();
        Auditoria = new AuditoriaMemoria();
    }

    public FuenteEspia Fuente { get; }

    public RosterFalso Roster { get; }

    public AuditoriaMemoria Auditoria { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // El flujo completo indexa el reglamento real del material (solo lectura); sin llave el LLM real responde 503.
        builder.UseSetting("KNOWLEDGE_PATH", RutaRepo.Reglamento);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAcademicDataSource>();
            services.RemoveAll<IRosterProvider>();
            services.RemoveAll<IAuditLog>();
            services.AddSingleton<IAcademicDataSource>(Fuente);
            services.AddSingleton<IRosterProvider>(Roster);
            services.AddSingleton<IAuditLog>(Auditoria);
            _extra?.Invoke(services);
        });
    }

    public static StringContent Json(string json, string mediaType = "application/json") =>
        new(json, Encoding.UTF8, mediaType);
}
