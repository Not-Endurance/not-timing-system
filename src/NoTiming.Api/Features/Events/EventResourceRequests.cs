using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>An Event is started from its Setup and takes nothing else: the document names the Setup by its id and carries no member.</summary>
internal sealed class EventStart : MemberAttributes, IResourceAttributes
{
    public static string? ResourceType => "events";

    public override JsonApiFailure? Invalid()
    {
        return Members is { Count: > 0 } named
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "An Event is started from its Setup and takes nothing else.",
                string.Join(", ", named.Keys)
            )
            : null;
    }
}

/// <summary>The members a change of an Event names. Its id, when the document gives one, is the one of the route.</summary>
internal sealed class EventChange : MemberAttributes, IResourceAttributes
{
    public static string? ResourceType => "events";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;
}
