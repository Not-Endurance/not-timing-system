using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// Creates the indexes the tenancy of #643 depends on when the host starts. One grant is one person in one place, and a
/// unique index says it for requests that race (a missing role of an Operator is a value like any other, so two Operators
/// of the same email cannot be linked to an Event twice). The others are the lookups: the grants of an account on an
/// Event, the invitations that wait for an email, the Events a Main Operator runs, and the accounts of a Tenant. Creating
/// an index that already exists with the same definition does nothing.
/// </summary>
internal sealed class TenancyIndexes : IHostedService
{
    readonly IMongoClient _client;
    readonly NIdentityOptions _options;

    public TenancyIndexes(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = _client.GetDatabase(_options.Database);
        var grants = database.GetCollection<BsonDocument>(TenantOwned.EVENT_GRANTS);
        await grants.Indexes.CreateManyAsync(
            [
                Index(
                    "grants_one_person_in_one_place",
                    new BsonDocument
                    {
                        { "EventId", 1 },
                        { "Kind", 1 },
                        { "OfficialRole", 1 },
                        { "Email", 1 },
                    },
                    unique: true
                ),
                Index("grants_of_an_account_on_an_event", new BsonDocument { { "EventId", 1 }, { "AccountId", 1 } }),
                Index("grants_waiting_for_an_email", new BsonDocument { { "Email", 1 }, { "AccountId", 1 } }),
            ],
            cancellationToken
        );
        await database
            .GetCollection<BsonDocument>(TenantOwned.CONFIGURE_EVENTS)
            .Indexes.CreateOneAsync(
                Index("events_by_main_operator", new BsonDocument("MainOperatorId", 1)),
                cancellationToken: cancellationToken
            );
        await database
            .GetCollection<BsonDocument>(TenantOwned.EVENT_INFORMATIONS)
            .Indexes.CreateOneAsync(
                Index("events_by_main_operator", new BsonDocument("MainOperatorId", 1)),
                cancellationToken: cancellationToken
            );
        await database
            .GetCollection<BsonDocument>(_options.UsersCollection)
            .Indexes.CreateOneAsync(
                Index("accounts_by_tenant", new BsonDocument("Memberships.TenantId", 1)),
                cancellationToken: cancellationToken
            );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    static CreateIndexModel<BsonDocument> Index(string name, BsonDocument keys, bool unique = false)
    {
        return new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = name, Unique = unique });
    }
}
