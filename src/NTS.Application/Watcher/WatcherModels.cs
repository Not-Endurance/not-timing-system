using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Application.Shared;
using NTS.Domain.Enums;
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
            Timestamp = snapshot.Timestamp?.ToString(),
        };
    }

    public int Number { get; set; }
    public string[] Names { get; set; } = [];
    public string? Timestamp { get; set; } = "";

    public Snapshot MapToDomain()
    {
        var athlete = new Person(Names);
        var timestamp = Timestamp == null ? null : new Timestamp(Timestamp);
        return new Snapshot(Number, athlete, timestamp);
    }

    public SnapshotModel Copy()
    {
        return new SnapshotModel
        {
            Number = Number,
            Names = [.. Names],
            Timestamp = Timestamp,
        };
    }
}

public class SnapshotGroupModel
{
    public static SnapshotGroupModel MapFrom(SnapshotGroup group)
    {
        return new SnapshotGroupModel
        {
            Entries = group.Entries.AsEnumerable().Select(SnapshotModel.MapFrom).ToArray(),
            Type = group.Type,
        };
    }

    public SnapshotModel[] Entries { get; set; } = [];
    public SnapshotType Type { get; set; }

    public SnapshotGroup MapToDomain()
    {
        var snapshots = Entries.Select(entry => entry.MapToDomain());
        return new SnapshotGroup(snapshots, Type);
    }

    public SnapshotGroupModel Copy()
    {
        return new SnapshotGroupModel { Entries = Entries.Select(entry => entry.Copy()).ToArray(), Type = Type };
    }
}

public class NtsUserSessionModel
    : NUserSessionModel<NtsUserSessionStateModel>,
        IDocument,
        IKrudModel<NtsUserSessionModel>
{
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;

    public void MapFrom(NtsUserSessionModel session)
    {
        Id = session.Id;
        TenantId = session.TenantId;
        UserIdentifier = session.UserIdentifier;
        ReplaceState(session.State);
    }

    public NtsUserSessionModel MapToEntity()
    {
        var model = new NtsUserSessionModel();
        model.MapFrom(this);
        return model;
    }

    public void ReplaceState(NtsUserSessionStateModel? state)
    {
        State = state?.Copy();
    }
}

public class NtsUserSessionStateModel
{
    public int? EventId { get; set; }
    public SnapshotGroupModel[] SnapshotHistory { get; set; } = [];

    public IReadOnlyList<SnapshotGroup> GetSnapshotHistory()
    {
        return SnapshotHistory.Select(x => x.MapToDomain()).ToArray();
    }

    public NtsUserSessionStateModel Copy()
    {
        return new NtsUserSessionStateModel
        {
            EventId = EventId,
            SnapshotHistory = SnapshotHistory.Select(group => group.Copy()).ToArray(),
        };
    }
}
