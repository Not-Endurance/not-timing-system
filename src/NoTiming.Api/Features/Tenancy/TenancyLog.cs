using NTS.Domain.Access;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The events that change who may do what (#643, ADR-0012), each with an id and a name that stay as they are, so that a
/// dashboard or an alert can key off them whatever the wording of a message becomes. They are the 11xx range, next to the
/// 10xx of signing in (<c>AuthEvents</c>).
/// </summary>
internal static class TenancyEvents
{
    public static readonly EventId EVENT_CREATED = new(1101, "EventCreated");
    public static readonly EventId MAIN_OPERATOR_ASSIGNED = new(1102, "MainOperatorAssigned");
    public static readonly EventId EVENT_HANDED_OVER = new(1103, "EventHandedOver");
    public static readonly EventId GRANT_LINKED = new(1104, "GrantLinked");
    public static readonly EventId GRANT_REMOVED = new(1105, "GrantRemoved");
    public static readonly EventId TENANT_RULES_EDITED = new(1106, "TenantRulesEdited");
    public static readonly EventId INVITATIONS_ATTACHED = new(1107, "InvitationsAttached");
    public static readonly EventId INVITATIONS_NOT_ATTACHED = new(1108, "InvitationsNotAttached");
}

/// <summary>
/// Writes those events. One carries the id of the user who did it and the ids of what it was done to, and what the Event
/// or the Tenant was left with. It never carries an email address, a name or anything else an account keeps: the log is
/// read by people who are not meant to read those, and a grant that is still pending is told by that, not by who it waits
/// for.
/// </summary>
internal sealed class TenancyLog
{
    readonly ILogger<TenancyLog> _logger;

    public TenancyLog(ILogger<TenancyLog> logger)
    {
        _logger = logger;
    }

    public void EventCreated(Guid user, Guid eventId, string tenantId)
    {
        _logger.LogInformation(
            TenancyEvents.EVENT_CREATED,
            "User {UserId} made Event {EventId} in Tenant {TenantId}.",
            user,
            eventId,
            tenantId
        );
    }

    public void MainOperatorAssigned(Guid user, Guid eventId, Guid mainOperator)
    {
        _logger.LogInformation(
            TenancyEvents.MAIN_OPERATOR_ASSIGNED,
            "User {UserId} assigned user {MainOperatorId} as the Main Operator of Event {EventId}.",
            user,
            mainOperator,
            eventId
        );
    }

    public void EventHandedOver(Guid user, Guid eventId, Guid mainOperator)
    {
        _logger.LogInformation(
            TenancyEvents.EVENT_HANDED_OVER,
            "User {UserId} handed Event {EventId} over to user {MainOperatorId}.",
            user,
            eventId,
            mainOperator
        );
    }

    public void GrantLinked(Guid user, EventGrant grant)
    {
        _logger.LogInformation(
            TenancyEvents.GRANT_LINKED,
            "User {UserId} gave grant {GrantId} on Event {EventId}: {Kind}, role {OfficialRole}, pending: {Pending}.",
            user,
            grant.Id,
            grant.EventId,
            grant.Kind,
            grant.OfficialRole?.ToString() ?? "none",
            grant.IsPending
        );
    }

    public void GrantRemoved(Guid user, Guid grantId, Guid eventId)
    {
        _logger.LogInformation(
            TenancyEvents.GRANT_REMOVED,
            "User {UserId} removed grant {GrantId} from Event {EventId}.",
            user,
            grantId,
            eventId
        );
    }

    public void TenantRulesEdited(Guid user, string tenantId, RegionalRules rules)
    {
        _logger.LogInformation(
            TenancyEvents.TENANT_RULES_EDITED,
            "User {UserId} edited the rules of Tenant {TenantId}: only average loop speed {OnlyAverageLoopSpeed}, ranker {RankerCode}.",
            user,
            tenantId,
            rules.OnlyAverageLoopSpeed,
            rules.RankerCode
        );
    }

    public void InvitationsAttached(Guid user, long count)
    {
        _logger.LogInformation(
            TenancyEvents.INVITATIONS_ATTACHED,
            "Pending grants taken by user {UserId} on proving an address: {Count}.",
            user,
            count
        );
    }

    public void InvitationsNotAttached(Guid user, Exception exception)
    {
        _logger.LogError(
            TenancyEvents.INVITATIONS_NOT_ATTACHED,
            exception,
            "The pending grants of user {UserId} could not be attached.",
            user
        );
    }
}
