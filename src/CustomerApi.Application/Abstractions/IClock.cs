namespace CustomerApi.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}