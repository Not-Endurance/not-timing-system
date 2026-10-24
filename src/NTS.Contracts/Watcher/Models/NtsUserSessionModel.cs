using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Watcher.Models;

public class NtsUserSessionModel
    : NUserSessionModel<NtsUserSessionStateModel>,
        IDocument,
        IKrudModel<NtsUserSessionModel>
{
    [BsonId]
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ObjectId MongoId { get; set; } = ObjectId.GenerateNewId();

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid EventId { get; set; }

    public void MapFrom(NtsUserSessionModel session)
    {
        MongoId = session.MongoId;
        Id = session.Id;
        TenantId = session.TenantId;
        EventId = session.EventId;
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
