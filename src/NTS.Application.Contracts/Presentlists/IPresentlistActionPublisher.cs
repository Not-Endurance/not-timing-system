using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Application.Contracts.Presentlists;

public interface IPresentlistActionPublisher
{
    Task PublishPresentation(PresentlistEntry entry);
    Task PublishPresentationAcknoledged(VetInAcknoledged acknoledgement);
}
