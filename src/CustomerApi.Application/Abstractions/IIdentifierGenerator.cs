namespace CustomerApi.Application.Abstractions;

public interface IIdentifierGenerator
{
    Guid NewId();
}