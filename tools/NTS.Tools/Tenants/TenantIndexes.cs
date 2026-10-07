using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.Tenants;

/// <summary>
/// The indexes that the hosts of the platform make when they start (<c>IdentityIndexInitializer</c> and
/// <c>TenancyIndexes</c>), made here before they do (#607): the unique ones can fail on data, such as two accounts that share
/// an email or two grants of one person, and failing here, with the report beside it, is better than a host that does not
/// start. An index that exists with the same definition is left alone, and the integration tests start both hosts' makers
/// over a database that this made, so the definitions cannot drift apart unnoticed.
/// </summary>
internal static class TenantIndexes
{
    public const string GRANTS = "event_grants";
    public const string GRANTS_UNIQUE = "grants_one_person_in_one_place";

    /// <summary>The names of the indexes the collection has, none when it is not there yet.</summary>
    public static async Task<HashSet<string>> ExistingAsync(IMongoCollection<BsonDocument> collection)
    {
        try
        {
            var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
            return [.. indexes.Select(x => x["name"].AsString)];
        }
        catch (MongoCommandException ex) when (ex.Code == 26)
        {
            return [];
        }
    }

    static FilterDefinitionBuilder<BsonDocument> Filter => Builders<BsonDocument>.Filter;

    /// <summary>Every index, by the collection that holds it, in the order they are made.</summary>
    public static IReadOnlyList<(string Collection, CreateIndexModel<BsonDocument> Index)> All { get; } =
        [
            (
                "users",
                Partial(
                    "identity_email_unique",
                    new BsonDocument("Email", 1),
                    Filter.Type("Email", BsonType.String),
                    unique: true
                )
            ),
            (
                "users",
                Partial(
                    "identity_external_login_unique",
                    new BsonDocument { { "ExternalProvider", 1 }, { "ExternalSubject", 1 } },
                    Filter.Type("ExternalSubject", BsonType.String),
                    unique: true
                )
            ),
            (
                "users",
                Partial(
                    "identity_passkey_credential_unique",
                    new BsonDocument("Passkeys.CredentialId", 1),
                    new BsonDocument("Passkeys.CredentialId", new BsonDocument("$exists", true)),
                    unique: true
                )
            ),
            ("auth_sessions", Ttl("sessions_expiry", "ExpiresAt")),
            ("auth_sessions", Plain("sessions_user", new BsonDocument("UserId", 1))),
            ("auth_challenges", Ttl("challenges_expiry", "ExpiresAt")),
            (
                "auth_challenges",
                Plain(
                    "challenges_email_purpose_unique",
                    new BsonDocument { { "Email", 1 }, { "Purpose", 1 } },
                    unique: true
                )
            ),
            (
                GRANTS,
                Plain(
                    GRANTS_UNIQUE,
                    new BsonDocument
                    {
                        { "EventId", 1 },
                        { "Kind", 1 },
                        { "OfficialRole", 1 },
                        { "Email", 1 },
                    },
                    unique: true
                )
            ),
            (
                GRANTS,
                Plain("grants_of_an_account_on_an_event", new BsonDocument { { "EventId", 1 }, { "AccountId", 1 } })
            ),
            (GRANTS, Plain("grants_waiting_for_an_email", new BsonDocument { { "Email", 1 }, { "AccountId", 1 } })),
            ("configure_events", Plain("events_by_main_operator", new BsonDocument("MainOperatorId", 1))),
            ("event_informations", Plain("events_by_main_operator", new BsonDocument("MainOperatorId", 1))),
            (
                "event_participations",
                Plain(
                    "participations_of_an_event_by_number",
                    new BsonDocument { { "EventId", 1 }, { "Combination.Number", 1 } }
                )
            ),
            ("event_participations", Plain("participations_by_time_event", new BsonDocument("Phases.Events._id", 1))),
            ("users", Plain("accounts_by_tenant", new BsonDocument("Memberships.TenantId", 1))),
        ];

    static CreateIndexModel<BsonDocument> Plain(string name, BsonDocument keys, bool unique = false)
    {
        return new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = name, Unique = unique });
    }

    static CreateIndexModel<BsonDocument> Ttl(string name, string field)
    {
        return new CreateIndexModel<BsonDocument>(
            new BsonDocument(field, 1),
            new CreateIndexOptions { Name = name, ExpireAfter = TimeSpan.Zero }
        );
    }

    static CreateIndexModel<BsonDocument> Partial(
        string name,
        BsonDocument keys,
        FilterDefinition<BsonDocument> filter,
        bool unique
    )
    {
        return new CreateIndexModel<BsonDocument>(
            keys,
            new CreateIndexOptions<BsonDocument>
            {
                Name = name,
                Unique = unique,
                PartialFilterExpression = filter,
            }
        );
    }
}
