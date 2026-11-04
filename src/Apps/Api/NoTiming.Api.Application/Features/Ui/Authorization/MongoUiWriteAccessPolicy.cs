using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NoTiming.Api.Application.Features.Ui.Authorization;

// TODO: Implement Search in IRepository abstraction and decouple the policy implementation from MongoDB
internal sealed class MongoUiWriteAccessPolicy : IReceiveSnapshotAccessPolicy
{
    const string DATABASE = "nts";
    const string USERS_COLLECTION = "users";
    const string OFFICIALS_COLLECTION = "event_officials";
    const string OPERATORS_COLLECTION = "event_operators";

    readonly IMongoContext _context;

    public MongoUiWriteAccessPolicy(IMongoContext context)
    {
        _context = context;
    }

    public async Task<bool> CanWriteSnapshots(string email, int eventId)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return false;
        }

        var database = _context.Client.GetDatabase(DATABASE);
        var userId = await database
            .GetCollection<UiUserDocument>(USERS_COLLECTION)
            .Find(x => x.Email == normalizedEmail)
            .Project(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (userId == null)
        {
            return false;
        }

        var canWriteAsOperator = await database
            .GetCollection<OperatorModel>(OPERATORS_COLLECTION)
            .Find(x => x.EventId == eventId && x.UserId == userId.Value && x.Role == OfficialRole.Steward)
            .AnyAsync();
        if (canWriteAsOperator)
        {
            return true;
        }

        var allowedOfficialRoles = SnapshotAccessPolicy.AllowedOfficialRoles;
        return await database
            .GetCollection<OfficialModel>(OFFICIALS_COLLECTION)
            .Find(x => x.EventId == eventId && x.UserId == userId.Value && allowedOfficialRoles.Contains(x.Role))
            .AnyAsync();
    }

    static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    sealed class UiUserDocument
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
    }
}
