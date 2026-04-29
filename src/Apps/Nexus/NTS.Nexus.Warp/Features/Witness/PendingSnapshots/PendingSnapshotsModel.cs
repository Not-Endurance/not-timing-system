using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Not.Structures;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.Warp.Features.Witness.PendingSnapshots;

public class PendingSnapshotsModel : IIdentifiable
{
    [BsonIgnore]
    public int Id => default;

    [BsonId]
    public ObjectId MongoId { get; set; }

    public string EnduranceEventId { get; set; } = "";
    public SnapshotGroupModel[] SnapshotGroups { get; set; } = [];
}
