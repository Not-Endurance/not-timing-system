using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Not.Structures;
using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Features.Witness;

public class PendingSnapshotsModel : IIdentifiable
{
    [BsonIgnore]
    public int Id => default;

    [BsonId]
    public ObjectId MongoId { get; set; }

    public string EnduranceEventId { get; set; } = "";
    public SnapshotGroupModel[] SnapshotGroups { get; set; } = [];
}
