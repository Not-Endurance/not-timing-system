using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;

namespace NTS.Nexus.Warp.Features.Witness.Authorization;

// TODO: Implement Search in IRepository abstraction and decouple the policy implementation from MongoDB
internal sealed class MongoReceiveSnapshotAccessPolicy : IReceiveSnapshotAccessPolicy
{
    const string DATABASE = "nts";
    const string USERS_COLLECTION = "users";
    const string OFFICIALS_COLLECTION = "officials";

    readonly IMongoContext _context;

    public MongoReceiveSnapshotAccessPolicy(IMongoContext context)
    {
        _context = context;
    }

    public async Task<bool> IsOfficial(string email, int eventId)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return false;
        }

        var database = _context.Client.GetDatabase(DATABASE);
        var userId = await database
            .GetCollection<WitnessUserDocument>(USERS_COLLECTION)
            .Find(x => x.Email == normalizedEmail)
            .Project(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (userId == null)
        {
            return false;
        }

        return await database
            .GetCollection<OfficialModel>(OFFICIALS_COLLECTION)
            .Find(x => x.EventId == eventId && !x.IsDeleted && x.UserId == userId.Value)
            .AnyAsync();
    }

    static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    sealed class WitnessUserDocument
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
    }
}
