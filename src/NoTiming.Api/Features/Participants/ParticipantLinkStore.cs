using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;

namespace NoTiming.Api.Features.Participants;

/// <summary>What a person has linked themselves to (#638, ADR-0010): the Athlete of the registry that is them, and the Horses of it that are theirs.</summary>
internal sealed class ParticipantLinks
{
    public ParticipantLinks(Guid? athleteId, IReadOnlyList<Guid> horseIds)
    {
        AthleteId = athleteId;
        HorseIds = horseIds;
    }

    public static ParticipantLinks None { get; } = new(null, []);

    public Guid? AthleteId { get; }

    /// <summary>The Horses the person added, in the order they added them.</summary>
    public IReadOnlyList<Guid> HorseIds { get; }
}

/// <summary>What linking a Horse to a person did.</summary>
internal enum HorseLinking
{
    Linked = 1,
    AlreadyLinked = 2,

    /// <summary>The person has as many Horses as they may.</summary>
    LimitReached = 3,
}

/// <summary>
/// The links of a person, in the <c>user_links</c> collection (#638): one document for each account, whose id is the
/// account's. They belong to the person and to no Tenant, as the registries they point into are read across Tenants. Every
/// operation names the owner and finds only that owner's document: there is no operation without one, so there is no way to
/// read or change the links of another person through this class, and the routes cannot forget to ask. A link is made or taken
/// away by one write of its own, so that two requests of a person at once do not undo one another.
/// </summary>
internal sealed class ParticipantLinkStore
{
    const string COLLECTION = "user_links";
    const string ATHLETE_ID = "AthleteId";
    const string HORSE_IDS = "HorseIds";

    readonly IMongoCollection<BsonDocument> _links;

    public ParticipantLinkStore(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _links = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(COLLECTION);
    }

    /// <summary>The owner's links; none for a person who has made no link.</summary>
    public async Task<ParticipantLinks> FindAsync(Guid owner, CancellationToken cancellationToken)
    {
        var document = await _links.Find(OwnedBy(owner)).FirstOrDefaultAsync(cancellationToken);
        if (document == null)
        {
            return ParticipantLinks.None;
        }

        var horses =
            document.TryGetValue(HORSE_IDS, out var ids) && ids.IsBsonArray
                ? ids
                    .AsBsonArray.OfType<BsonBinaryData>()
                    .Where(x => x.SubType == BsonBinarySubType.UuidStandard)
                    .Select(x => x.ToGuid(GuidRepresentation.Standard))
                : [];
        return new ParticipantLinks(BsonGuids.Of(document, ATHLETE_ID), [.. horses]);
    }

    /// <summary>Makes the Athlete the one the owner is, in place of the one they were. False when they were it already.</summary>
    public async Task<bool> LinkAthleteAsync(Guid owner, Guid athleteId, CancellationToken cancellationToken)
    {
        var before = await _links.FindOneAndUpdateAsync(
            OwnedBy(owner),
            Builders<BsonDocument>.Update.Set(ATHLETE_ID, BsonGuids.Binary(athleteId)),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.Before },
            cancellationToken
        );
        return before == null || BsonGuids.Of(before, ATHLETE_ID) != athleteId;
    }

    /// <summary>Takes the link away when the owner is that Athlete. False when they were not.</summary>
    public async Task<bool> UnlinkAthleteAsync(Guid owner, Guid athleteId, CancellationToken cancellationToken)
    {
        var result = await _links.UpdateOneAsync(
            OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq(ATHLETE_ID, BsonGuids.Binary(athleteId)),
            Builders<BsonDocument>.Update.Unset(ATHLETE_ID),
            cancellationToken: cancellationToken
        );
        return result.ModifiedCount > 0;
    }

    /// <summary>Adds the Horse to the owner's, unless they have as many as the limit.</summary>
    public async Task<HorseLinking> LinkHorseAsync(
        Guid owner,
        Guid horseId,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var horse = BsonGuids.Binary(horseId);
        if (
            await _links
                .Find(OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq(HORSE_IDS, horse))
                .AnyAsync(cancellationToken)
        )
        {
            return HorseLinking.AlreadyLinked;
        }

        try
        {
            // the filter finds the document only while it holds fewer Horses than the limit, and makes one when there is none
            await _links.UpdateOneAsync(
                OwnedBy(owner) & Builders<BsonDocument>.Filter.Exists($"{HORSE_IDS}.{limit - 1}", false),
                Builders<BsonDocument>.Update.AddToSet(HORSE_IDS, horse),
                new UpdateOptions { IsUpsert = true },
                cancellationToken
            );
            return HorseLinking.Linked;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // the document is there and full, so the filter did not find it and the upsert met its id
            return HorseLinking.LimitReached;
        }
    }

    /// <summary>Takes the Horse away from the owner's. False when it was not one of them.</summary>
    public async Task<bool> UnlinkHorseAsync(Guid owner, Guid horseId, CancellationToken cancellationToken)
    {
        var horse = BsonGuids.Binary(horseId);
        var result = await _links.UpdateOneAsync(
            OwnedBy(owner) & Builders<BsonDocument>.Filter.Eq(HORSE_IDS, horse),
            Builders<BsonDocument>.Update.Pull(HORSE_IDS, horse),
            cancellationToken: cancellationToken
        );
        return result.ModifiedCount > 0;
    }

    static FilterDefinition<BsonDocument> OwnedBy(Guid owner)
    {
        return Builders<BsonDocument>.Filter.Eq("_id", BsonGuids.Binary(owner));
    }
}
