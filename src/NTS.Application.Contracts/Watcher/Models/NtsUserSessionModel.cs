using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Application.Contracts.Watcher.Models;

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
