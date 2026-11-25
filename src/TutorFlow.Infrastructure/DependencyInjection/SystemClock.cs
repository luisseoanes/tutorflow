using TutorFlow.Application.Ports;

namespace TutorFlow.Infrastructure.DependencyInjection;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
