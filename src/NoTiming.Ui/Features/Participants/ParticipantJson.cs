using System.Text.Json;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Participants;

/// <summary>What the Api tells of a Participant (#638), read into the types of the Ui.</summary>
internal static class ParticipantJson
{
    /// <summary>A Participant told as a member of a document: its <c>kind</c>, <c>id</c> and <c>displayName</c>.</summary>
    public static ParticipantName NameIn(JsonElement member)
    {
        return new ParticipantName(
            Enum.Parse<ParticipantKind>(member.GetProperty("kind").GetString()!),
            Guid.Parse(member.GetProperty("id").GetString()!),
            member.GetProperty("displayName").GetString()!
        );
    }

    /// <summary>A Participant told as a resource: its <c>id</c>, and its <c>kind</c> and <c>displayName</c> as attributes.</summary>
    public static ParticipantName NameOf(JsonElement resource)
    {
        var attributes = resource.GetProperty("attributes");
        return new ParticipantName(
            Enum.Parse<ParticipantKind>(attributes.GetProperty("kind").GetString()!),
            Guid.Parse(resource.GetProperty("id").GetString()!),
            attributes.GetProperty("displayName").GetString()!
        );
    }

    /// <summary>A Participation of a history told as a resource: its <c>id</c> and what it says in its attributes.</summary>
    public static HistoricParticipation ParticipationOf(JsonElement resource)
    {
        var attributes = resource.GetProperty("attributes");
        return new HistoricParticipation(
            Guid.Parse(resource.GetProperty("id").GetString()!),
            Guid.Parse(attributes.GetProperty("eventId").GetString()!),
            attributes.GetProperty("eventName").GetString()!,
            attributes.GetProperty("location").GetString()!,
            attributes.GetProperty("startDay").GetDateTimeOffset(),
            attributes.GetProperty("competition").GetString()!,
            NameIn(attributes.GetProperty("other")),
            PlacingIn(attributes),
            attributes.TryGetProperty("alsoCountedIn", out var also) ? [.. also.EnumerateArray().Select(CountedIn)] : []
        );
    }

    /// <summary>Whether a page of a list says there is a next one.</summary>
    public static bool HasNext(JsonElement document)
    {
        return document.TryGetProperty("links", out var links)
            && links.TryGetProperty("next", out var next)
            && next.ValueKind == JsonValueKind.String;
    }

    static CountedPlacing CountedIn(JsonElement ranking)
    {
        return new CountedPlacing(
            ranking.GetProperty("name").GetString()!,
            ranking.GetProperty("category").GetString()!,
            PlacingIn(ranking)
        );
    }

    /// <summary>The <c>placing</c> a member tells, none when it tells none or one that is not known.</summary>
    static Placing? PlacingIn(JsonElement owner)
    {
        if (!owner.TryGetProperty("placing", out var placing))
        {
            return null;
        }

        return placing.GetProperty("kind").GetString() switch
        {
            nameof(PlacingKind.Ranked) => Placing.Ranked(placing.GetProperty("rank").GetInt32()),
            nameof(PlacingKind.Eliminated) => Placing.Eliminated(placing.GetProperty("code").GetString()!),
            nameof(PlacingKind.NotRanked) => Placing.NotRanked(),
            _ => null,
        };
    }
}
