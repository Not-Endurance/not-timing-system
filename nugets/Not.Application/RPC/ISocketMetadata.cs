using Not.Injection;

namespace Not.Application.RPC;

public interface ISocketMetadata
{
    string? ConnectionGroupKey { get; }
}
