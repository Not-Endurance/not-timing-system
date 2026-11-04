using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using NC.Application.Authentication.User;
using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;

namespace NoTiming.Application.Contracts.Watcher.Models;

public class NoTimingUserSessionModel
    : NUserSessionModel<NoTimingUserSessionStateModel>,
        IDocument,
        IKrudModel<NoTimingUserSessionModel>
{
    [BsonId]
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ObjectId MongoId { get; set; } = ObjectId.GenerateNewId();

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }

    public void MapFrom(NoTimingUserSessionModel session)
    {
        MongoId = session.MongoId;
        Id = session.Id;
        TenantId = session.TenantId;
        EventId = session.EventId;
        UserIdentifier = session.UserIdentifier;
        ReplaceState(session.State);
    }

    public NoTimingUserSessionModel MapToEntity()
    {
        var model = new NoTimingUserSessionModel();
        model.MapFrom(this);
        return model;
    }

    public void ReplaceState(NoTimingUserSessionStateModel? state)
    {
        State = state?.Copy();
    }
}
