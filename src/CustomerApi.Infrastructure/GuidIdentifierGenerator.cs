using CustomerApi.Application.Abstractions;

namespace CustomerApi.Infrastructure;

public sealed class GuidIdentifierGenerator : IIdentifierGenerator
{
    public Guid NewId() => Guid.NewGuid();
}