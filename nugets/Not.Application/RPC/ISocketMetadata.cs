using Not.Injection;

namespace Not.Application.RPC;

public interface ISocketMetadata : ISingleton
{
    string? ConnectionGroupKey { get; }
}
