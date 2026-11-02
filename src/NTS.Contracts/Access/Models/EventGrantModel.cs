using System.Text.Json.Serialization;
using Not.Krud.Abstractions;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NTS.Contracts.Access.Models;

/// <summary>
/// A grant as the Api serves it at <c>/api/event-grants</c>: the Event, the kind, the role of an Official, and the person by
/// their email or by their account. The display name and whether the grant is pending are the Api's to say: they are read,
/// and what a write names that the Api does not take is ignored (ADR-0017). The Tenant is not in the answer, so a grant that
/// is read is shown as a model, and <see cref="MapToEntity"/> is for a grant that is written with its Tenant.
/// </summary>
public class EventGrantModel : IKrudModel<EventGrant>
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? TenantId { get; set; }
    public GrantKind Kind { get; set; }
    public OfficialRole? OfficialRole { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }
    public string? DisplayName { get; set; }

    [JsonPropertyName("pending")]
    public bool IsPending { get; set; }

    public void MapFrom(EventGrant grant)
    {
        Id = grant.Id;
        EventId = grant.EventId;
        TenantId = grant.TenantId;
        Kind = grant.Kind;
        OfficialRole = grant.OfficialRole;
        Email = grant.Email;
        AccountId = grant.AccountId;
        IsPending = grant.IsPending;
    }

    public EventGrant MapToEntity()
    {
        var tenant = TenantId ?? string.Empty;
        return Kind switch
        {
            GrantKind.Official => EventGrant.ForOfficial(
                Id,
                EventId,
                tenant,
                OfficialRole ?? throw new ArgumentException("An Official has a role.", nameof(OfficialRole)),
                Email ?? string.Empty,
                AccountId
            ),
            GrantKind.ConsoleOperator => EventGrant.ForConsoleOperator(
                Id,
                EventId,
                tenant,
                Email ?? string.Empty,
                AccountId
            ),
            _ => EventGrant.ForOperator(Id, EventId, tenant, Email ?? string.Empty, AccountId),
        };
    }
}
