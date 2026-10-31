using Not.Krud.Abstractions;
using NTS.Domain.Access;

namespace NTS.Contracts.Access.Models;

/// <summary>
/// A Console Operator as the Api serves it at <c>/api/console-operators</c>. A Console Operator is a grant of the kind
/// <see cref="GrantKind.ConsoleOperator"/> (ADR-0012), so it is the same entity as a grant; the Api answers with the display
/// name and whether the grant is pending as well. A Console Operator is assigned by the account of a person, or by the email
/// of one who has none yet: exactly one of the two is named.
/// </summary>
public class ConsoleOperatorModel : IKrudModel<EventGrant>
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? TenantId { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }
    public string? DisplayName { get; set; }
    public bool Pending { get; set; }

    public void MapFrom(EventGrant grant)
    {
        Id = grant.Id;
        EventId = grant.EventId;
        TenantId = grant.TenantId;
        Email = grant.Email;
        AccountId = grant.AccountId;
        Pending = grant.IsPending;
    }

    public EventGrant MapToEntity()
    {
        return EventGrant.ForConsoleOperator(Id, EventId, TenantId ?? string.Empty, Email ?? string.Empty, AccountId);
    }
}
