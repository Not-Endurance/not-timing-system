using MongoDB.Driver;
using NC.Server.Authentication.Interface;
using NC.Storage.Mongo;
using NoTiming.Application.Contracts.Account;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Enums;

namespace NoTiming.Api.Application.Features.Ui.Authorization;

// TODO: Implement Search in IRepository abstraction and decouple the policy implementation from MongoDB
internal sealed class MongoUiReceiveAccessPolicy : IReceiveSnapshotAccessPolicy
{
    const string DATABASE = "nts";
    const string OFFICIALS_COLLECTION = "event_officials";
    const string OPERATORS_COLLECTION = "event_operators";

    readonly IMongoContext _context;
    readonly IUserDirectory<NoTimingProfile> _users;

    public MongoUiReceiveAccessPolicy(IMongoContext context, IUserDirectory<NoTimingProfile> users)
    {
        _context = context;
        _users = users;
    }

    public async Task<bool> CanReceiveSnapshots(string email, int eventId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var user = await _users.FindByEmail(email);
        if (user is null)
        {
            return false;
        }

        var userId = user.Id;
        var database = _context.Client.GetDatabase(DATABASE);
        var canWriteAsOperator = await database
            .GetCollection<OperatorModel>(OPERATORS_COLLECTION)
            .Find(x => x.EventId == eventId && x.UserId == userId && x.Role == OfficialRole.Steward)
            .AnyAsync();
        if (canWriteAsOperator)
        {
            return true;
        }

        // Built with the filter builder: the LINQ translator fails on the captured SnapshotAccessPolicy.AllowedOfficialRoles array.
        var filters = Builders<OfficialModel>.Filter;
        var officialFilter = filters.And(
            filters.Eq(x => x.EventId, eventId),
            filters.Eq(x => x.UserId, userId),
            filters.In(x => x.Role, SnapshotAccessPolicy.AllowedOfficialRoles)
        );
        return await database.GetCollection<OfficialModel>(OFFICIALS_COLLECTION).Find(officialFilter).AnyAsync();
    }
}
