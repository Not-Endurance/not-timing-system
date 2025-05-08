using Not.Injection;

namespace Not.Application.RPC;

public interface IRpcMetadata : ISingleton
{
    string? ConnectionGroupKey { get; }
}
