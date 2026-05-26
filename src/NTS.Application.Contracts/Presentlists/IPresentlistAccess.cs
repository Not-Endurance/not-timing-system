using Not.Application.Behinds.Adapters;

namespace NTS.Application.Contracts.Presentlists;

public interface IPresentlistAccess : IStatefulService
{
    bool CanAcknowledgePresentations { get; }
}
