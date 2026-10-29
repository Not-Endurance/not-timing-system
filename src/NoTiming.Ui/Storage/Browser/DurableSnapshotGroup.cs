using System.Text.Json;
using System.Text.Json.Serialization;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Storage.Browser;

/// <summary>
/// The group that was sent and not answered for good, as the device backs it up (#645, ADR-0013): its id, its kind and its
/// Snapshots, with the names the page shows and the instants they were made at. Each group is kept under its own id, so that
/// several groups wait together while the API is down. The person and the Event it is for are kept with it, and only they get it
/// back: nobody else who uses the device sends it. Nothing of the session is in it.
/// </summary>
public sealed class DurableSnapshotGroup
{
    /// <summary>The prefix of the key each group is kept under in the local storage of the browser.</summary>
    public const string KEY_PREFIX = "nts.durable-snapshots.";

    public static DurableSnapshotGroup Of(SnapshotGroup group, Guid accountId, Guid eventId)
    {
        return new DurableSnapshotGroup
        {
            Id = group.Id,
            AccountId = accountId,
            EventId = eventId,
            Kind = group.Type,
            Entries =
            [
                .. group.Entries.Select(x => new DurableSnapshot
                {
                    Number = x.Number,
                    Name = x.Name,
                    NameEnglish = x.NameEnglish,
                    Ruleset = x.Ruleset,
                    Time = x.Timestamp!.ToDateTimeOffset(),
                }),
            ],
        };
    }

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid EventId { get; set; }
    public SnapshotType Kind { get; set; }
    public List<DurableSnapshot> Entries { get; set; } = [];

    /// <summary>The group, or none when what was kept is not one that can be sent: it names no Snapshot, or one with no name.</summary>
    public SnapshotGroup? ToGroup()
    {
        if (Id == Guid.Empty || Entries.Count == 0 || Entries.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            return null;
        }

        return new SnapshotGroup(
            Entries.Select(x => new Snapshot(x.Number, x.Name, x.NameEnglish, new Timestamp(x.Time), x.Ruleset)),
            Kind,
            Id
        );
    }

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class DurableSnapshot
{
    public int Number { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public DateTimeOffset Time { get; set; }
}
