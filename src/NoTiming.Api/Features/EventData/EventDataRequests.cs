using System.Text.Json;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The document of a row that is made: the members of the row as they were sent, and the Event it names, which is where the
/// row is made. The id of it is the one the client made for the row, a Guid, and may be left out.
/// </summary>
internal sealed class NewRow : NewMembers, INamesEvent
{
    const string EVENT_ID = "eventId";

    /// <summary>
    /// The Event the document names, as the text it gave; none when it names none. It is no member of the class, or the
    /// <c>eventId</c> of the document would be skipped instead of kept with the members the row is made of.
    /// </summary>
    string? INamesEvent.EventId =>
        (Members ?? []).FirstOrDefault(x => string.Equals(x.Key, EVENT_ID, StringComparison.OrdinalIgnoreCase))
            is { Key: not null } named
            ? named.Value.ValueKind switch
            {
                JsonValueKind.String => named.Value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => named.Value.GetRawText(),
            }
            : null;

    /// <summary>The Event is named once: the caller is let in to one, and the row is made in the one that was named.</summary>
    public override JsonApiFailure? Invalid()
    {
        // The Event is read twice, by the filter (any spelling of a Guid) and into the row (the one the Api writes), so it has to
        // be spelled the one way for both to mean the same Event.
        var named = ((INamesEvent)this).EventId;
        return
            (Members ?? []).Keys.Count(x => string.Equals(x, EVENT_ID, StringComparison.OrdinalIgnoreCase)) > 1
            || (named != null && !Guid.TryParseExact(named, "D", out _))
            ? JsonApiFailures.MalformedRequest.With("The Event is named once, as an id.")
            : null;
    }
}
