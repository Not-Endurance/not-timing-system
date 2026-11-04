using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Objects.Payloads;

namespace NoTiming.EventBus.Contracts.Features.Ui.Procedures;

public interface IUiHubProcedures
{
    Task Receive(NoTimingRequest<SnapshotGroupModel> request);
    Task AcknoledgeVetIn(NoTimingRequest<VetInAcknoledged> request);
}
