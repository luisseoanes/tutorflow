using Microsoft.Extensions.Hosting;

namespace TutorFlow.Infrastructure.Roster;

/// <summary>Carga el roster al arrancar y lo recarga cada TTL en segundo plano (SPEC 6.6).</summary>
public sealed class RosterRefreshService : BackgroundService
{
    private static readonly TimeSpan ReintentoSinDatos = TimeSpan.FromSeconds(10);
    private readonly RosterProvider _roster;
    private readonly TimeSpan _ttl;

    public RosterRefreshService(RosterProvider roster, TimeSpan ttl)
    {
        _roster = roster;
        _ttl = ttl;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var ok = await _roster.CargarAsync(stoppingToken);
            var espera = ok || _roster.TieneSnapshot ? _ttl : (_ttl < ReintentoSinDatos ? _ttl : ReintentoSinDatos);
            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
