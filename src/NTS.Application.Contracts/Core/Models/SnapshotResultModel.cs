using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Core.Models;

public class SnapshotResultModel : IEventScoped, ISoftDeletableDocument, IKrudModel<SnapshotResult>
{
    public static SnapshotResultModel From(SnapshotResult result)
    {
        var model = new SnapshotResultModel();
        model.MapFrom(result);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public CoreSnapshotModel Snapshot { get; set; } = default!;
    public SnapshotResultType Type { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(SnapshotResult result)
    {
        Id = result.Id;
        EventId = result.EventId;
        Snapshot = CoreSnapshotModel.MapFrom(result.Snapshot);
        Type = result.Type;
    }

    public SnapshotResult MapToEntity()
    {
        return new SnapshotResult(Snapshot.MapToEntity(), Type, EventId, Id);
    }
}
