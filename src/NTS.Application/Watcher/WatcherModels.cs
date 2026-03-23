using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Application.Shared;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Application.Watcher;

public class SnapshotModel
{
    public static SnapshotModel MapFrom(Snapshot snapshot)
    {
        return new SnapshotModel
        {
            Number = snapshot.Number,
            Names = snapshot.Athlete.Names,
            Timestamp = snapshot.Timestamp.ToString(),
        };
    }

    public int Number { get; set; }
    public string[] Names { get; set; } = [];
    public string Timestamp { get; set; } = "";

    public Snapshot MapToDomain()
    {
        var athlete = new Person(Names);
        var timestamp = new Timestamp(Timestamp);
        return new Snapshot(Number, athlete, timestamp);
    }
}

public class SnapshotGroupModel
{
    public static SnapshotGroupModel MapFrom(SnapshotGroup group)
    {
        return new SnapshotGroupModel
        {
            Entries = group.Entries.AsEnumerable().Select(SnapshotModel.MapFrom).ToArray(),
            Type = group.Type.ToString(),
        };
    }

    public SnapshotModel[] Entries { get; set; } = [];
    public string Type { get; set; } = "";

    public SnapshotGroup MapToDomain()
    {
        var snapshots = Entries.Select(entry => entry.MapToDomain());
        return new SnapshotGroup(snapshots, Type);
    }
}

public class NtsUserSessionModel : NUserSessionModel<NtsUserSessionStateModel>, IDocument, IKrudModel<NtsUserSessionModel>
{
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int? EventId
    {
        get => State?.EventId;
        set => ResolveState().EventId = value;
    }

    public SnapshotGroupModel[] SnapshotHistory
    {
        get => State?.SnapshotHistory ?? [];
        set => ResolveState().SnapshotHistory = value;
    }

    public IReadOnlyList<SnapshotGroup> GetSnapshotHistory()
    {
        return SnapshotHistory.Select(x => x.MapToDomain()).ToArray();
    }

    public void MapFrom(NtsUserSessionModel session)
    {
        Id = session.Id;
        UserIdentifier = session.UserIdentifier;
        State =
            session.State == null
                ? null
                : new NtsUserSessionStateModel
                {
                    EventId = session.State.EventId,
                    SnapshotHistory = [.. session.State.SnapshotHistory],
                };
    }

    public NtsUserSessionModel MapToEntity()
    {
        var model = new NtsUserSessionModel();
        model.MapFrom(this);
        return model;
    }

    NtsUserSessionStateModel ResolveState()
    {
        State ??= new NtsUserSessionStateModel();
        return State;
    }
}

public class NtsUserSessionStateModel
{
    public int? EventId { get; set; }
    public SnapshotGroupModel[] SnapshotHistory { get; set; } = [];
}
