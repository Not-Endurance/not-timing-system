using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Not.Structures;

namespace NTS.Application.Contracts.Watcher.Models;

public class PendingSnapshotsModel : IIdentifiable
{
    [BsonIgnore]
    public int Id => default;

    [BsonId]
    public ObjectId MongoId { get; set; }

    public int EventId { get; set; }
    public SnapshotGroupModel[] SnapshotGroups { get; set; } = [];
}
