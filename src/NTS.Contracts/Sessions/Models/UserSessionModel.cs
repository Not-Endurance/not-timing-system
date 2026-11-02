using Not.Krud.Abstractions;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Core.Sessions;

namespace NTS.Contracts.Sessions.Models;

/// <summary>
/// What a person keeps for an Event as the Api serves it at <c>/api/user-sessions</c>: the Event and the state, which is the
/// groups sent and the Snapshots selected. The owner is the account the Api finds the record by, and is not in the model.
/// </summary>
public class UserSessionModel : IKrudModel<UserSession>
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public NtsUserSessionStateModel State { get; set; } = new();

    public void MapFrom(UserSession session)
    {
        Id = session.Id;
        EventId = session.EventId;
        State = new NtsUserSessionStateModel
        {
            SnapshotHistory = [.. session.SnapshotHistory.Select(SnapshotGroupModel.MapFrom)],
            SnapshotSelections = [.. session.SnapshotSelections.Select(SnapshotModel.MapFrom)],
        };
    }

    public UserSession MapToEntity()
    {
        return new UserSession(Id, EventId, State.GetSnapshotHistory(), State.GetSnapshotSelections());
    }
}
