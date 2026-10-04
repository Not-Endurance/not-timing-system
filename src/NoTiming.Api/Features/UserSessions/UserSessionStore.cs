using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.UserSessions;

/// <summary>One person's state for one Event, as the routes hand it out.</summary>
internal sealed class UserSession
{
    public UserSession(Guid id, Guid eventId, NtsUserSessionStateModel state)
    {
        Id = id;
        EventId = eventId;
        State = state;
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public NtsUserSessionStateModel State { get; }
}

/// <summary>
/// The state a person keeps per Event, in the <c>event_user_sessions</c> collection (#602). Every operation names the
/// owner and finds only that owner's records: there is no operation without one, so there is no way to read, change or
/// delete the record of another person through this class, and the routes cannot forget to ask. The records have the
/// shape the Functions API writes (PascalCase members, enums as text, no member that is null), which is mapped by hand
/// here because that shape comes from conventions of the Functions host that this host does not share. The owner is
/// the account id; the constant Tenant of today is written until the Tenants of ADR-0012 reach the data.
/// </summary>
internal sealed class UserSessionStore
{
    const string COLLECTION = "event_user_sessions";
    const string LEGACY_TENANT = "nts";

    readonly IMongoCollection<BsonDocument> _records;

    public UserSessionStore(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _records = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(COLLECTION);
    }

    /// <summary>The owner's records, of one Event when it is named.</summary>
    public async Task<IReadOnlyList<UserSession>> ListAsync(
        string owner,
        Guid? eventId,
        CancellationToken cancellationToken
    )
    {
        var filter = OwnedBy(owner);
        if (eventId != null)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("EventId", Binary(eventId.Value));
        }

        var records = await _records.Find(filter).ToListAsync(cancellationToken);
        return [.. records.Select(Read).OfType<UserSession>()];
    }

    public async Task<UserSession?> FindAsync(string owner, Guid id, CancellationToken cancellationToken)
    {
        var record = await _records
            .Find(OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq("Id", Binary(id)))
            .FirstOrDefaultAsync(cancellationToken);
        return record == null ? null : Read(record);
    }

    /// <summary>
    /// The owner's record for the Event, made when there is none. A record that is there is returned as it is, and the
    /// state offered is not put over it. Created tells which of the two happened.
    /// </summary>
    public async Task<(UserSession Session, bool Created)> MakeAsync(
        string owner,
        Guid eventId,
        NtsUserSessionStateModel? state,
        CancellationToken cancellationToken
    )
    {
        var id = Guid.NewGuid();

        // A record that has no id (the Functions API leaves out an id that is empty) cannot be addressed by anything,
        // so it is not the owner's record for the Event: one is made, and the other is left where it is.
        var record = await _records.FindOneAndUpdateAsync(
            OwnedBy(owner)
                & Builders<BsonDocument>.Filter.Eq("EventId", Binary(eventId))
                & Builders<BsonDocument>.Filter.Exists("Id"),
            Builders<BsonDocument>
                .Update.SetOnInsert("Id", Binary(id))
                .SetOnInsert("TenantId", LEGACY_TENANT)
                .SetOnInsert("State", StateToBson(state ?? new NtsUserSessionStateModel())),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken
        );
        var session = Read(record) ?? throw new InvalidOperationException("The record that was made cannot be read.");
        return (session, session.Id == id);
    }

    /// <summary>Puts the state in place of the one the owner's record has; null when they have no such record.</summary>
    public async Task<UserSession?> ReplaceStateAsync(
        string owner,
        Guid id,
        NtsUserSessionStateModel state,
        CancellationToken cancellationToken
    )
    {
        var record = await _records.FindOneAndUpdateAsync(
            OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq("Id", Binary(id)),
            Builders<BsonDocument>.Update.Set("State", StateToBson(state)),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken
        );
        return record == null ? null : Read(record);
    }

    public async Task<bool> DeleteAsync(string owner, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _records.DeleteOneAsync(
            OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq("Id", Binary(id)),
            cancellationToken
        );
        return deleted.DeletedCount > 0;
    }

    static FilterDefinition<BsonDocument> OwnedBy(string owner)
    {
        return Builders<BsonDocument>.Filter.Eq("UserIdentifier", owner);
    }

    static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    /// <summary>The record as a session, or null when it has no id or no Event that is one: nothing could ask for it.</summary>
    static UserSession? Read(BsonDocument record)
    {
        if (!TryGuid(record, "Id", out var id) || !TryGuid(record, "EventId", out var eventId))
        {
            return null;
        }

        return new UserSession(
            id,
            eventId,
            record.TryGetValue("State", out var state) && state.IsBsonDocument
                ? StateFromBson(state.AsBsonDocument)
                : new NtsUserSessionStateModel()
        );
    }

    static bool TryGuid(BsonDocument record, string field, out Guid value)
    {
        value = Guid.Empty;
        if (
            record.TryGetValue(field, out var member)
            && member is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
        )
        {
            value = binary.ToGuid(GuidRepresentation.Standard);
        }

        return value != Guid.Empty;
    }

    static BsonDocument StateToBson(NtsUserSessionStateModel state)
    {
        return new BsonDocument
        {
            ["SnapshotHistory"] = new BsonArray(state.SnapshotHistory.Select(GroupToBson)),
            ["SnapshotSelections"] = new BsonArray(state.SnapshotSelections.Select(SnapshotToBson)),
        };
    }

    static BsonDocument GroupToBson(SnapshotGroupModel group)
    {
        return new BsonDocument
        {
            ["Entries"] = new BsonArray(group.Entries.Select(SnapshotToBson)),
            ["Type"] = group.Type.ToString(),
        };
    }

    static BsonDocument SnapshotToBson(SnapshotModel snapshot)
    {
        var document = new BsonDocument { ["Number"] = snapshot.Number, ["Name"] = snapshot.Name };
        if (snapshot.NameEnglish != null)
        {
            document["NameEnglish"] = snapshot.NameEnglish;
        }

        if (snapshot.Ruleset != null)
        {
            document["Ruleset"] = snapshot.Ruleset.ToString();
        }

        if (snapshot.Timestamp != null)
        {
            document["Timestamp"] = snapshot.Timestamp;
        }

        return document;
    }

    static NtsUserSessionStateModel StateFromBson(BsonDocument state)
    {
        return new NtsUserSessionStateModel
        {
            SnapshotHistory = [.. ArrayOf(state, "SnapshotHistory").Select(x => GroupFromBson(x.AsBsonDocument))],
            SnapshotSelections =
            [
                .. ArrayOf(state, "SnapshotSelections").Select(x => SnapshotFromBson(x.AsBsonDocument)),
            ],
        };
    }

    static SnapshotGroupModel GroupFromBson(BsonDocument group)
    {
        return new SnapshotGroupModel
        {
            Entries = [.. ArrayOf(group, "Entries").Select(x => SnapshotFromBson(x.AsBsonDocument))],
            Type = EnumOf<SnapshotType>(group, "Type") ?? default,
        };
    }

    static SnapshotModel SnapshotFromBson(BsonDocument snapshot)
    {
        return new SnapshotModel
        {
            Number = snapshot.TryGetValue("Number", out var number) && number.IsNumeric ? number.ToInt32() : 0,
            Name = TextOf(snapshot, "Name") ?? string.Empty,
            NameEnglish = TextOf(snapshot, "NameEnglish"),
            Ruleset = EnumOf<CompetitionRuleset>(snapshot, "Ruleset"),
            Timestamp = TextOf(snapshot, "Timestamp"),
        };
    }

    static IEnumerable<BsonValue> ArrayOf(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsBsonArray
            ? value.AsBsonArray.Where(x => x.IsBsonDocument)
            : [];
    }

    static string? TextOf(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;
    }

    /// <summary>An enum as the Functions API writes it, text, or as an older record may hold it, a number.</summary>
    static T? EnumOf<T>(BsonDocument document, string field)
        where T : struct, Enum
    {
        if (!document.TryGetValue(field, out var value))
        {
            return null;
        }

        if (value.IsString && Enum.TryParse<T>(value.AsString, out var named))
        {
            return named;
        }

        return value.IsInt32 && Enum.IsDefined(typeof(T), value.AsInt32)
            ? (T)Enum.ToObject(typeof(T), value.AsInt32)
            : null;
    }
}
