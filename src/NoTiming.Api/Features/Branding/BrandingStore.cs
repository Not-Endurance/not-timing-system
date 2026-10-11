using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Tenancy;

namespace NoTiming.Api.Features.Branding;

/// <summary>Which of the two places on a document a logo is for: the one at the left of its header and the one at the right.</summary>
internal enum LogoPosition
{
    Left = 0,
    Right = 1,
}

/// <summary>
/// The logos a Tenant keeps for itself and the ones an Event keeps in place of them (#646, ADR-0012), in the Tenant's collection
/// <c>brandings</c>: one document for the Tenant and one for each Event that has an override, each with at most a logo for each
/// position. They are written as one change, so that a request that sets one and clears the other is made whole or not at all.
/// A logo is read when a document is printed, and never with the rest of an Event, so that it weighs nothing on every other read.
/// </summary>
internal sealed class BrandingStore
{
    const string LEFT = "Left";
    const string RIGHT = "Right";
    const string CONTENT_TYPE = "ContentType";
    const string CONTENT = "Content";
    const string UPDATED = "UpdatedAt";

    public static string KeyOfTenant(string tenantId)
    {
        return $"tenant:{tenantId}";
    }

    public static string KeyOfEvent(Guid eventId)
    {
        return $"event:{eventId}";
    }

    readonly TenantCollections _tenants;
    readonly TimeProvider _time;

    public BrandingStore(TenantCollections tenants, TimeProvider time)
    {
        _tenants = tenants;
        _time = time;
    }

    /// <summary>The logos the document has, by position; a position that has none is not in it.</summary>
    public async Task<IReadOnlyDictionary<LogoPosition, Logo>> ReadAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken
    )
    {
        var document = await Open(tenantId).FindOneAsync(Key(key), cancellationToken);
        var logos = new Dictionary<LogoPosition, Logo>();
        if (document is null)
        {
            return logos;
        }

        foreach (var (position, member) in Members())
        {
            if (document.TryGetValue(member, out var value) && value is BsonDocument logo && Logo(logo) is { } read)
            {
                logos[position] = read;
            }
        }

        return logos;
    }

    /// <summary>Sets the logos named and clears the ones named as none, in one change; a position that is not named stays as it is.</summary>
    public async Task SetAsync(
        string tenantId,
        string key,
        IReadOnlyDictionary<LogoPosition, Logo?> changes,
        CancellationToken cancellationToken
    )
    {
        if (changes.Count == 0)
        {
            return;
        }

        var collection = Open(tenantId);
        var update = Builders<BsonDocument>.Update.Set(UPDATED, _time.GetUtcNow().UtcDateTime);
        foreach (var (position, logo) in changes)
        {
            var member = Members().Single(x => x.Position == position).Member;
            update = logo is null
                ? update.Unset(member)
                : update.Set(
                    member,
                    new BsonDocument
                    {
                        { CONTENT_TYPE, logo.ContentType },
                        { CONTENT, new BsonBinaryData(logo.Content) },
                    }
                );
        }

        if (await collection.UpdateAsync(Key(key), update, cancellationToken))
        {
            return;
        }

        if (changes.Values.All(x => x is null))
        {
            return; // there is nothing to clear in a document that is not there
        }

        var made = new BsonDocument { { "_id", key } };
        foreach (var (position, logo) in changes.Where(x => x.Value is not null))
        {
            var member = Members().Single(x => x.Position == position).Member;
            made[member] = new BsonDocument
            {
                { CONTENT_TYPE, logo!.ContentType },
                { CONTENT, new BsonBinaryData(logo.Content) },
            };
        }

        made[UPDATED] = _time.GetUtcNow().UtcDateTime;
        try
        {
            await collection.InsertAsync(made, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // another request made it between the change and this: the change is made on what it made
            await collection.UpdateAsync(Key(key), update, cancellationToken);
        }
    }

    /// <summary>Removes what an Event kept for itself, which goes with it when it is deleted before it started.</summary>
    public async Task RemoveOfEventAsync(string tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        await Open(tenantId).DeleteManyAsync(Key(KeyOfEvent(eventId)), cancellationToken);
    }

    static IEnumerable<(LogoPosition Position, string Member)> Members()
    {
        yield return (LogoPosition.Left, LEFT);
        yield return (LogoPosition.Right, RIGHT);
    }

    static FilterDefinition<BsonDocument> Key(string key)
    {
        return Builders<BsonDocument>.Filter.Eq("_id", key);
    }

    static Logo? Logo(BsonDocument document)
    {
        return
            document.TryGetValue(CONTENT_TYPE, out var type)
            && type.IsString
            && document.TryGetValue(CONTENT, out var content)
            && content.IsBsonBinaryData
            ? new Logo(type.AsString, content.AsBsonBinaryData.Bytes)
            : null;
    }

    TenantCollection Open(string tenantId)
    {
        return _tenants.Of(TenantOwned.BRANDINGS, tenantId);
    }
}
