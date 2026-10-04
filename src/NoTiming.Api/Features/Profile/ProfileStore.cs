using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;

namespace NoTiming.Api.Features.Profile;

/// <summary>
/// Saves the profile of a person into their own user document (#602). It sets the fields of the profile by name and
/// touches nothing else of the document: not the Tenant the person was placed in, not their roles, not what identity
/// keeps. The fields and their shape are those the Functions API writes, because both read the same documents until it
/// is retired, and an optional field that was cleared is not written at all, as that API does not write a null.
/// </summary>
internal sealed class ProfileStore
{
    readonly IMongoCollection<BsonDocument> _users;

    public ProfileStore(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _users = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(options.Value.UsersCollection);
    }

    /// <summary>
    /// Writes the fields given, and only those: a text sets the field, null removes it. Two edits of different members
    /// that cross each other both stay, because neither writes what it did not change.
    /// </summary>
    public async Task SaveAsync(
        Guid userId,
        IReadOnlyDictionary<string, string?> fields,
        CancellationToken cancellationToken
    )
    {
        if (fields.Count == 0)
        {
            return;
        }

        var update = Builders<BsonDocument>.Update.Combine(
            fields.Select(x =>
                x.Value == null
                    ? Builders<BsonDocument>.Update.Unset(x.Key)
                    : Builders<BsonDocument>.Update.Set(x.Key, x.Value)
            )
        );
        await _users.UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(userId, GuidRepresentation.Standard)),
            update,
            cancellationToken: cancellationToken
        );
    }
}
