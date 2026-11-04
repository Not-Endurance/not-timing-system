using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Domain.Core.Objects.Presentlists;

namespace NoTiming.Application.Contracts.Presentlists;

public interface IPresentlistActionPublisher
{
    Task PublishPresentation(PresentlistEntry entry);
    Task PublishPresentationAcknoledged(VetInAcknoledged acknoledgement);
}
