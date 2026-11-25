namespace TutorFlow.Infrastructure.MockApi;

public sealed class MockApiOptions
{
    public const string DefaultBaseUrl = "http://mock-api:4000";

    public string BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>Timeout por intento (SPEC 10.2: 3 s).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Reintentos adicionales tras el primer intento (SPEC 10.2: 2), solo GET.</summary>
    public int Reintentos { get; init; } = 2;

    /// <summary>Espera base del backoff exponencial: base, 2 x base, ...</summary>
    public TimeSpan BackoffBase { get; init; } = TimeSpan.FromMilliseconds(200);
}
