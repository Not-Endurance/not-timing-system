using NC.Application.Behinds.Adapters;

namespace NoTiming.Application.Contracts.Presentlists;

public interface IPresentlistAccess : IStatefulService
{
    bool CanAcknowledgePresentations { get; }
}
