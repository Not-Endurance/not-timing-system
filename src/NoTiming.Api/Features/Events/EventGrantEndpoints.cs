using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The Main Operator links people to the Officials and Operators of its Event (#643, ADR-0012): by the exact email, or by
/// the id of an account found by name. An email that has an account gives that account the grant at once, and one that has
/// none is a pending invitation that attaches when the person registers. A grant is of a kind the Event has, an Operator or
/// an Official of a role, and never a role of the Tenant or of the platform. The emails that come back are masked.
/// </summary>
internal static class EventGrantEndpoints
{
    const string EVENT_GRANTS = "event-grants";
    const string EVENT_ID = "eventId";

    public static IEndpointRouteBuilder MapEventGrants(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/event-grants", Link).RequireAuthorization();
        app.MapGet("/api/event-grants", List).RequireAuthorization();
        app.MapDelete("/api/event-grants/{id}", Remove).RequireAuthorization();
        return app;
    }

    static async Task<IResult> Link(
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventGrantStore grants,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<GrantAttributes>(context.Request, EVENT_GRANTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var attributes = read.Attributes!;
        if (!Guid.TryParse(attributes.EventId, out var eventId))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-event",
                "A grant is for an Event, named by its id as eventId."
            );
        }

        var facts = await events.FindAsync(eventId, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = AccessPolicy.Decide(
            Capability.LinkAccounts,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var shape = Shape(attributes);
        if (shape.Refusal != null)
        {
            return shape.Refusal;
        }

        NIdentityUser? account;
        string email;
        if (attributes.AccountId is { } accountId)
        {
            account = await users.FindByIdAsync(accountId.ToString());
            if (account?.Email is null)
            {
                return JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "account-not-found",
                    "There is no such account."
                );
            }

            email = account.Email;
        }
        else
        {
            email = attributes.Email!.Trim();
            account = await users.FindByEmailAsync(users.NormalizeEmail(email)!);
        }

        (EventGrant Grant, bool Created, bool Attached) linked;
        try
        {
            linked = await grants.LinkAsync(
                facts.Record,
                shape.Kind,
                shape.Role,
                email,
                account?.Id,
                context.RequestAborted
            );
        }
        catch (GrantLimitReachedException)
        {
            return JsonApiResults.Error(
                StatusCodes.Status422UnprocessableEntity,
                "grant-limit-reached",
                $"An Event has at most {EventGrantStore.MAX_GRANTS_PER_EVENT} grants: remove one that is not needed."
            );
        }

        var (grant, created, attached) = linked;
        if (created || attached)
        {
            log.GrantLinked(user.Id, grant);
        }

        return GrantResource(grant, account, created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }

    static async Task<IResult> List(
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventGrantStore grants
    )
    {
        var query = JsonApiQuery.Read(context.Request, EVENT_ID);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (!query.EqualTo.TryGetValue(EVENT_ID, out var text) || !Guid.TryParse(text, out var eventId))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "The grants of one Event are listed: use filter=eventId eq <id>."
            );
        }

        var facts = await events.FindAsync(eventId, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        // Seeing who has access is the Main Operator's, however far on the Event is: only the role counts here.
        var verdict = AccessPolicy.Decide(
            Capability.LinkAccounts,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed && verdict.Reason != Refusal.EventEnded)
        {
            return AccessResults.Refused(verdict);
        }

        var items = new List<(string Id, object Attributes)>();
        foreach (var grant in await grants.ListAsync(facts.Record, context.RequestAborted))
        {
            var account = grant.AccountId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
            items.Add((grant.Id.ToString(), AttributesOf(grant, account)));
        }

        return JsonApiResults.Collection(EVENT_GRANTS, items);
    }

    static async Task<IResult> Remove(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventGrantStore grants,
        TenancyLog log
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var grant = Guid.TryParse(id, out var grantId) ? await grants.FindAsync(grantId, context.RequestAborted) : null;
        var facts = grant == null ? null : await events.FindAsync(grant.EventId, context.RequestAborted);
        if (grant is null || facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = AccessPolicy.Decide(
            Capability.LinkAccounts,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            // A grant is the Main Operator's to see: to anybody else it is not there, as it is not for a missing one.
            return verdict.Reason == Refusal.EventEnded ? AccessResults.Refused(verdict) : JsonApiResults.NotFound();
        }

        if (!await grants.RemoveAsync(grant, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        log.GrantRemoved(user.Id, grant.Id, grant.EventId);
        return Results.NoContent();
    }

    /// <summary>What the document says a grant is: its kind, and for an Official its role; one way of naming the person.</summary>
    static GrantShape Shape(GrantAttributes attributes)
    {
        if (attributes.Others is { Count: > 0 })
        {
            return GrantShape.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "unsupported-attribute",
                    "A grant names an Event, a kind, a role for an Official, and a person by an email or an account.",
                    string.Join(", ", attributes.Others.Keys)
                )
            );
        }

        var kind = attributes.Kind?.Trim().ToLowerInvariant() switch
        {
            "operator" => (GrantKind?)GrantKind.Operator,
            "official" => GrantKind.Official,
            _ => null,
        };
        if (kind == null)
        {
            return GrantShape.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-kind",
                    "A grant is for an operator or an official."
                )
            );
        }

        OfficialRole? role = null;
        if (kind == GrantKind.Official)
        {
            if (
                !Enum.TryParse<OfficialRole>(attributes.OfficialRole, ignoreCase: false, out var parsed)
                || !Enum.IsDefined(parsed)
            )
            {
                return GrantShape.Refused(InvalidRole("An official has a role of the domain, such as Steward."));
            }

            role = parsed;
        }
        else if (attributes.OfficialRole != null)
        {
            return GrantShape.Refused(InvalidRole("An operator has no role."));
        }

        if ((attributes.Email == null) == (attributes.AccountId == null))
        {
            return GrantShape.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "malformed-request",
                    "Name the person by an email or by an account, and by one of them."
                )
            );
        }

        if (attributes.Email != null && !CodeSignIn.IsValidEmail(attributes.Email))
        {
            return GrantShape.Refused(
                JsonApiResults.Error(StatusCodes.Status400BadRequest, "invalid-email", "Enter a valid email address.")
            );
        }

        return GrantShape.Of(kind.Value, role);
    }

    static IResult InvalidRole(string title)
    {
        return JsonApiResults.Error(StatusCodes.Status400BadRequest, "invalid-role", title);
    }

    static IResult GrantResource(EventGrant grant, NIdentityUser? account, int status)
    {
        return JsonApiResults.Resource(status, EVENT_GRANTS, grant.Id.ToString(), AttributesOf(grant, account));
    }

    static object AttributesOf(EventGrant grant, NIdentityUser? account)
    {
        return new
        {
            eventId = grant.EventId,
            kind = grant.Kind.ToString().ToLowerInvariant(),
            officialRole = grant.OfficialRole?.ToString(),
            pending = grant.IsPending,
            accountId = grant.AccountId,
            displayName = account?.TextOf("Name"),
            email = EmailMask.Of(grant.Email),
        };
    }

    sealed class GrantShape
    {
        public static GrantShape Of(GrantKind kind, OfficialRole? role)
        {
            return new GrantShape(kind, role, null);
        }

        public static GrantShape Refused(IResult refusal)
        {
            return new GrantShape(default, null, refusal);
        }

        GrantShape(GrantKind kind, OfficialRole? role, IResult? refusal)
        {
            Kind = kind;
            Role = role;
            Refusal = refusal;
        }

        public GrantKind Kind { get; }
        public OfficialRole? Role { get; }
        public IResult? Refusal { get; }
    }
}

/// <summary>What a grant is sent as. A member that is not one of these is kept, to be refused.</summary>
internal sealed class GrantAttributes
{
    public string? EventId { get; set; }
    public string? Kind { get; set; }
    public string? OfficialRole { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}
