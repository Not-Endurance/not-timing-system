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
/// emails that come back are masked.
/// </summary>
internal static class ConsoleOperatorEndpoints
{
    const string CONSOLE_OPERATORS = "console-operators";
    const string EVENT_ID = "eventId";
    const string EMAIL = "email";

    public static IEndpointRouteBuilder MapConsoleOperators(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/console-operators", Assign).RequireAuthorization();
        app.MapGet("/api/console-operators", List).RequireAuthorization();
        app.MapDelete("/api/console-operators/{id}", Remove).RequireAuthorization();
        return app;
    }

    static async Task<IResult> Assign(
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        EventStore events,
        EventGrantStore grants,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<ConsoleOperatorAttributes>(context.Request, CONSOLE_OPERATORS);
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
                "A Console Operator is for an Event, named by its id as eventId."
            );
        }

        var facts = await events.FindAsync(eventId, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = await standing.DecideAtEventAsync(
            Capability.AssignConsoleOperators,
            user,
            facts,
            context.RequestAborted
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (Shape(attributes) is { } refusal)
        {
            return refusal;
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

        (EventGrant Grant, bool Created, bool Attached) assigned;
        try
        {
            assigned = await grants.LinkAsync(
                facts.Record,
                GrantKind.ConsoleOperator,
                null,
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

        var (grant, created, attached) = assigned;
        if (created || attached)
        {
            log.GrantLinked(user.Id, grant);
        }

        return JsonApiResults.Resource(
            created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            CONSOLE_OPERATORS,
            grant.Id.ToString(),
            AttributesOf(grant, account)
        );
    }

    static async Task<IResult> List(
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        EventStore events,
        EventGrantStore grants
    )
    {
        var query = JsonApiQuery.ReadList(context.Request, EMAIL, EVENT_ID);
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
                "The Console Operators of one Event are listed: use filter=eventId eq <id>."
            );
        }

        var facts = await events.FindAsync(eventId, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = await standing.DecideAtEventAsync(
            Capability.ReadConfiguration,
            user,
            facts,
            context.RequestAborted
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var items = new List<(string Id, object Attributes)>();
        var (page, hasMore) = await grants.ListAsync(
            facts.Record,
            consoleOperators: true,
            query.Descending,
            query.Skip,
            query.Size,
            context.RequestAborted
        );
        foreach (var grant in page)
        {
            var account = grant.AccountId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
            items.Add((grant.Id.ToString(), AttributesOf(grant, account)));
        }

        return JsonApiResults.Collection(
            CONSOLE_OPERATORS,
            items,
            links: JsonApiPaging.Links(context.Request, query.Number, hasMore),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }

    static async Task<IResult> Remove(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
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
        if (grant is null || facts is null || grant.Kind != GrantKind.ConsoleOperator)
        {
            return JsonApiResults.NotFound();
        }

        // A Console Operator is a Console Operator's and Root's to see: to anybody else it is not there, as it is not for a
        // missing one. One who sees it and is not Root is told what is missing.
        var seen = await standing.DecideAtEventAsync(Capability.ReadConfiguration, user, facts, context.RequestAborted);
        if (!seen.IsAllowed)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = await standing.DecideAtEventAsync(
            Capability.AssignConsoleOperators,
            user,
            facts,
            context.RequestAborted
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (!await grants.RemoveAsync(grant, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        log.GrantRemoved(user.Id, grant.Id, grant.EventId);
        return Results.NoContent();
    }

    /// <summary>What the document says a Console Operator is: an Event and one way of naming the person. None when it is whole.</summary>
    static IResult? Shape(ConsoleOperatorAttributes attributes)
    {
        if (attributes.Others is { Count: > 0 })
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "A Console Operator names an Event and a person by an email or an account.",
                string.Join(", ", attributes.Others.Keys)
            );
        }

        if ((attributes.Email == null) == (attributes.AccountId == null))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "Name the person by an email or by an account, and by one of them."
            );
        }

        return attributes.Email != null && !CodeSignIn.IsValidEmail(attributes.Email)
            ? JsonApiResults.Error(StatusCodes.Status400BadRequest, "invalid-email", "Enter a valid email address.")
            : null;
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

/// <summary>What a Console Operator is sent as. A member that is not one of these is kept, to be refused.</summary>
internal sealed class ConsoleOperatorAttributes
{
    public string? EventId { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}
