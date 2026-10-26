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
/// A Console Operator of an Event, or Root, links people to its Officials and Operators (#643, ADR-0012): by the exact email,
/// or by the id of an account found by name. An email that has an account gives that account the grant at once, and one that
/// has none is a pending invitation that attaches when the person registers. A grant is of a kind the Event has, an Operator
/// or an Official of a role, and never a role of the Tenant or of the platform; a Console Operator is a grant of its own
/// resource (<see cref="ConsoleOperatorEndpoints"/>). The emails that come back are masked. The list of the grants of an
/// Event is in the order of the emails, or the other way round (<c>sort</c>), a page at a time. Who may do what is declared on
/// each handler and decided by the endpoint filter (#671): a handler is reached by a caller who holds the Level, with a
/// request that is valid.
/// </summary>
internal static class EventGrantEndpoints
{
    const string EVENT_GRANTS = "event-grants";
    const string EVENT_ID = "eventId";
    const string EMAIL = "email";

    public static IEndpointRouteBuilder MapEventGrants(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/event-grants", Link);
        app.MapGet("/api/event-grants", List);
        app.MapDelete("/api/event-grants/{id}", Remove);
        return app;
    }

    [Requires(Capability.ConfigureEvent, PlaceIn.EventInBody)]
    static async Task<IResult> Link(
        JsonApiBody<GrantAttributes> body,
        AccessContext access,
        UserManager<NIdentityUser> users,
        EventGrantStore grants,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var attributes = body.Attributes;
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

        var (grant, created, attached) = await grants.LinkAsync(
            access.Event.Record,
            attributes.GrantKind,
            attributes.Role,
            email,
            account?.Id,
            cancellationToken
        );
        if (created || attached)
        {
            log.GrantLinked(access.User.Id, grant);
        }

        return GrantResource(grant, account, created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }

    // Seeing who has access is a Console Operator's and Root's, however far on the Event is.
    [Requires(Capability.ReadConfiguration, PlaceIn.EventInFilter)]
    static async Task<IResult> List(
        [ListOf(EMAIL, EVENT_ID)] ListQuery query,
        HttpRequest request,
        AccessContext access,
        UserManager<NIdentityUser> users,
        EventGrantStore grants,
        CancellationToken cancellationToken
    )
    {
        var items = new List<(string Id, object Attributes)>();
        var (page, hasMore) = await grants.ListAsync(
            access.Event.Record,
            consoleOperators: false,
            query.Descending,
            query.Skip,
            query.Size,
            cancellationToken
        );
        foreach (var grant in page)
        {
            var account = grant.AccountId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
            items.Add((grant.Id.ToString(), AttributesOf(grant, account)));
        }

        return JsonApiResults.Collection(
            EVENT_GRANTS,
            items,
            links: JsonApiPaging.Links(request, query.Number, hasMore),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }

    // A grant is a Console Operator's to see: to anybody else it is not there, as it is not for a missing one.
    [Requires(Capability.ConfigureEvent, PlaceIn.EventOfGrant, SeenWith = Capability.ReadConfiguration)]
    static async Task<IResult> Remove(
        AccessContext access,
        EventGrantStore grants,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var grant = access.Grant!;
        if (!await grants.RemoveAsync(grant, cancellationToken))
        {
            return JsonApiResults.NotFound();
        }

        log.GrantRemoved(access.User.Id, grant.Id, grant.EventId);
        return Results.NoContent();
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
}

/// <summary>
/// What a grant is sent as: an Event, a kind, a role for an Official, and a person by an email or an account. A member that
/// is not one of these is kept, to be refused.
/// </summary>
internal sealed class GrantAttributes : IResourceAttributes, INamesEvent
{
    public static string? ResourceType => "event-grants";

    public string? EventId { get; set; }
    public string? Kind { get; set; }
    public string? OfficialRole { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    /// <summary>The kind of grant, once the attributes are valid.</summary>
    [JsonIgnore]
    public GrantKind GrantKind =>
        Kind!.Trim().ToLowerInvariant() == "official" ? GrantKind.Official : GrantKind.Operator;

    /// <summary>The role of the Official, once the attributes are valid; none for an Operator.</summary>
    [JsonIgnore]
    public OfficialRole? Role => GrantKind == GrantKind.Official ? Enum.Parse<OfficialRole>(OfficialRole!) : null;

    public JsonApiFailure? Invalid()
    {
        if (Others is { Count: > 0 })
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "A grant names an Event, a kind, a role for an Official, and a person by an email or an account.",
                string.Join(", ", Others.Keys)
            );
        }

        var kind = Kind?.Trim().ToLowerInvariant();
        if (kind is not ("operator" or "official"))
        {
            return Failure("invalid-kind", "A grant is for an operator or an official.");
        }

        if (kind == "official")
        {
            if (
                !Enum.TryParse<OfficialRole>(OfficialRole, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed)
            )
            {
                return Failure("invalid-role", "An official has a role of the domain, such as Steward.");
            }
        }
        else if (OfficialRole != null)
        {
            return Failure("invalid-role", "An operator has no role.");
        }

        if ((Email == null) == (AccountId == null))
        {
            return Failure("malformed-request", "Name the person by an email or by an account, and by one of them.");
        }

        return Email != null && !CodeSignIn.IsValidEmail(Email)
            ? Failure("invalid-email", "Enter a valid email address.")
            : null;
    }

    static JsonApiFailure Failure(string code, string title)
    {
        return new JsonApiFailure(StatusCodes.Status400BadRequest, code, title);
    }
}
