using Not.Krud.Abstractions;
using NTS.Application.Shared;
using NTS.Domain.Core;
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

public class UserSessionModel : IDocument, IKrudModel<UserSessionModel>, ICoreSession
{
    public static UserSessionModel From(ICoreSession session, int id)
    {
        var model = new UserSessionModel
        {
            Id = id,
            EventId = session.EventId,
            SnapshotHistory = session.SnapshotHistory.Select(SnapshotGroupModel.MapFrom).ToArray(),
        };
        return model;
    }

    IReadOnlyList<SnapshotGroup> ICoreSession.SnapshotHistory => SnapshotHistory.Select(x => x.MapToDomain()).ToArray();
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int? EventId { get; set; }
    public SnapshotGroupModel[] SnapshotHistory { get; set; } = [];

    public void MapFrom(UserSessionModel session)
    {
        Id = session.Id;
        EventId = session.EventId;
        SnapshotHistory = [.. session.SnapshotHistory];
    }

    public UserSessionModel MapToEntity()
    {
        var model = new UserSessionModel();
        model.MapFrom(this);
        return model;
    }
}
