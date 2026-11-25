namespace TutorFlow.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
