namespace TutorFlow.Application.Ports;

public sealed record Fragmento(string ChunkId, string Documento, string Seccion, string Texto);

/// <summary>Recuperacion documental del reglamento. Los fragmentos son datos no confiables.</summary>
public interface IRetriever
{
    Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct);
}
