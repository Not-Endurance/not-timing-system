using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// Root assigns the Console Operators of an Event (#671, ADR-0012): the <c>console-operators</c> resource, a grant of its own
/// kind, named by the exact email or by the id of an account found by name. An email that has an account gives that account
/// the grant at once, and one that has none is a pending invitation that attaches when the person registers. An Event has
/// none until Root assigns one, and needs none. Root and the Console Operators of an Event list them, in the order of the
/// emails or the other way round (<c>sort</c>), a page at a time; only Root assigns and removes, until the Event ends. The
/// emails that come back are masked. Who may do what is declared on each handler and decided by the endpoint filter.
/// </summary>
internal static class ConsoleOperatorEndpoints
{
    const string CONSOLE_OPERATORS = "console-operators";
    const string EVENT_ID = "eventId";
    const string EMAIL = "email";

    public static IEndpointRouteBuilder MapConsoleOperators(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/console-operators", Assign);
        app.MapGet("/api/console-operators", List);
        app.MapDelete("/api/console-operators/{id}", Remove);
        return app;
    }

    [Requires(Capability.AssignConsoleOperators, PlaceIn.EventInBody)]
    static async Task<IResult> Assign(
        JsonApiBody<ConsoleOperatorAttributes> body,
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
            GrantKind.ConsoleOperator,
            null,
            email,
            account?.Id,
            cancellationToken
        );
        if (created || attached)
        {
            log.GrantLinked(access.User.Id, grant);
        }

        return JsonApiResults.Resource(
            created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            CONSOLE_OPERATORS,
            grant.Id.ToString(),
            AttributesOf(grant, account)
        );
    }

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
            consoleOperators: true,
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
            CONSOLE_OPERATORS,
            items,
            links: JsonApiPaging.Links(request, query.Number, hasMore),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }

    // A Console Operator is a Console Operator's and Root's to see: to anybody else it is not there, as it is not for a
    // missing one. One who sees it and is not Root is told what is missing.
    [Requires(
        Capability.AssignConsoleOperators,
        PlaceIn.EventOfConsoleOperator,
        SeenWith = Capability.ReadConfiguration
    )]
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

    static object AttributesOf(EventGrant grant, NIdentityUser? account)
    {
        return new
        {
            eventId = grant.EventId,
            pending = grant.IsPending,
            accountId = grant.AccountId,
            displayName = account?.TextOf("Name"),
            email = EmailMask.Of(grant.Email),
        };
    }
}

/// <summary>What a Console Operator is sent as: an Event, and a person by an email or an account. A member that is not one of these is kept, to be refused.</summary>
internal sealed class ConsoleOperatorAttributes : IResourceAttributes, INamesEvent
{
    public static string? ResourceType => "console-operators";

    public string? EventId { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    public JsonApiFailure? Invalid()
    {
        if (Others is { Count: > 0 })
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "A Console Operator names an Event and a person by an email or an account.",
                string.Join(", ", Others.Keys)
            );
        }

        if ((Email == null) == (AccountId == null))
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "Name the person by an email or by an account, and by one of them."
            );
        }

        return Email != null && !CodeSignIn.IsValidEmail(Email)
            ? new JsonApiFailure(StatusCodes.Status400BadRequest, "invalid-email", "Enter a valid email address.")
            : null;
    }
}
