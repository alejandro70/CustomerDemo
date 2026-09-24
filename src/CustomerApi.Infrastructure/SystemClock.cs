using CustomerApi.Application.Abstractions;

namespace CustomerApi.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}