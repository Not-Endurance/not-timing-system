using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Not.Injection;
using NoTiming.Ui.Features.Core.Dashboard;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Storage.Browser;

/// <summary>
/// The group that was sent and not answered, in the local storage of the browser (#645). Nothing of the session is kept
/// there: a key of the person and the Event, and the group as the device made it. A storage that cannot be used (a private
/// window, a full quota) keeps nothing, and the group is then sent again from the page as long as the page is open.
/// </summary>
public sealed class BrowserUnansweredSnapshots : IUnansweredSnapshots, IScoped
{
    const string GET_ITEM = "localStorage.getItem";
    const string SET_ITEM = "localStorage.setItem";
    const string REMOVE_ITEM = "localStorage.removeItem";
    static readonly JsonSerializerOptions OPTIONS = CreateOptions();

    readonly IJSRuntime _browser;

    public BrowserUnansweredSnapshots(IJSRuntime browser)
    {
        _browser = browser;
    }

    public async Task<SnapshotGroup?> Read(Guid accountId, Guid eventId)
    {
        var key = KeyOf(accountId, eventId);
        try
        {
            var text = await _browser.InvokeAsync<string?>(GET_ITEM, key);
            if (text == null)
            {
                return null;
            }

            var kept = JsonSerializer.Deserialize<Kept>(text, OPTIONS);
            var group = kept?.ToGroup();
            if (group == null)
            {
                await Forget(accountId, eventId); // what is there is not a group: it would not be read the next time either
            }

            return group;
        }
        catch (Exception ex) when (ex is JsonException or JSException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task Keep(Guid accountId, Guid eventId, SnapshotGroup group)
    {
        try
        {
            await _browser.InvokeVoidAsync(
                SET_ITEM,
                KeyOf(accountId, eventId),
                JsonSerializer.Serialize(Kept.Of(group), OPTIONS)
            );
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // The storage is not there to be used: the group is sent from the page, and not from the next one.
        }
    }

    public async Task Forget(Guid accountId, Guid eventId)
    {
        try
        {
            await _browser.InvokeVoidAsync(REMOVE_ITEM, KeyOf(accountId, eventId));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // See Keep.
        }
    }

    static string KeyOf(Guid accountId, Guid eventId)
    {
        return $"nts.unanswered-snapshots.{accountId}.{eventId}";
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

    sealed class Kept
    {
        public static Kept Of(SnapshotGroup group)
        {
            return new Kept
            {
                Id = group.Id,
                Kind = group.Type,
                Entries =
                [
                    .. group.Entries.Select(x => new KeptSnapshot
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

        public Guid Id { get; set; }
        public SnapshotType Kind { get; set; }
        public List<KeptSnapshot> Entries { get; set; } = [];

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
    }

    sealed class KeptSnapshot
    {
        public int Number { get; set; }
        public string Name { get; set; } = default!;
        public string? NameEnglish { get; set; }
        public CompetitionRuleset? Ruleset { get; set; }
        public DateTimeOffset Time { get; set; }
    }
}
