using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Not.Structures;

namespace NTS.Contracts.Watcher.Models;

public class PendingSnapshotsModel : IIdentifiable
{
    [BsonIgnore]
    public Guid Id => default;

    [BsonId]
    public ObjectId MongoId { get; set; }

    public Guid EventId { get; set; }
    public SnapshotGroupModel[] SnapshotGroups { get; set; } = [];
}
