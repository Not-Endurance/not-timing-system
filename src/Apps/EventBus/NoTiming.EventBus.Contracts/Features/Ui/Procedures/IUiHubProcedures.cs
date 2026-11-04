using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Payloads;

namespace NoTiming.EventBus.Contracts.Features.Ui.Procedures;

public interface IUiHubProcedures
{
    Task Receive(NoTimingRequest<SnapshotGroupModel> request);
    Task AcknoledgeVetIn(NoTimingRequest<VetInAcknoledged> request);
}
