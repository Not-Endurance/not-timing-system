using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A database as it was before Tenants (#607): accounts that Identity has not touched, countries, the documents that every
/// Tenant will own all stamped with the constant Tenant, Events, the state of persons keyed by whatever identified them,
/// and the collection of settings. It is stored the way the code before stored it, so that <c>migrate-tenants</c> meets what
/// it will meet.
/// </summary>
internal sealed class LegacyTenantData
{
    public const string BULGARIA_ID = "country-bg";

    public static BsonBinaryData Uuid(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    public LegacyTenantData(IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<BsonDocument> Collection(string name)
    {
        return Database.GetCollection<BsonDocument>(name);
    }

    public async Task<Guid> CountryAsync(string name, string? isoCode, string? nfCode = null)
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Uuid(id) },
            { "TenantId", "nts" },
            { "Name", name },
        };
        if (isoCode != null)
        {
            document["IsoCode"] = isoCode;
        }

        if (nfCode != null)
        {
            document["NfCode"] = nfCode;
        }

        await Collection("countries").InsertOneAsync(document);
        return id;
    }

    public async Task BulgariaAsync()
    {
        await CountryAsync("Bulgaria", "BG", "BUL");
    }

    /// <summary>An account of before Identity: the fields the application kept, an email as it was typed, and nothing of Identity.</summary>
    public async Task<Guid> AccountAsync(
        string? email,
        string? country = null,
        Action<BsonDocument>? more = null,
        Guid? id = null
    )
    {
        id ??= Guid.NewGuid();
        var document = new BsonDocument { { "_id", Uuid(id.Value) }, { "Roles", new BsonArray() } };
        if (email != null)
        {
            document["Email"] = email;
        }

        if (country != null)
        {
            document["CountryRegion"] = country;
        }

        document["Name"] = "Legacy " + id.Value.ToString("N")[..6];
        document["Unknown"] = "kept"; // something the application keeps that Identity does not know
        more?.Invoke(document);
        await Collection("users").InsertOneAsync(document);
        return id.Value;
    }

    /// <summary>A document of a collection a Tenant will own, stamped with the constant Tenant, or with the one given, or with none.</summary>
    public async Task<Guid> OwnedAsync(string collection, string? tenant = "nts", Action<BsonDocument>? more = null)
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument { { "_id", Uuid(id) }, { "Name", "of " + collection } };
        if (tenant != null)
        {
            document["TenantId"] = tenant;
        }

        more?.Invoke(document);
        await Collection(collection).InsertOneAsync(document);
        return id;
    }

    /// <summary>
    /// The Setup of an Event, with the Officials and the Operators it names by the email of the user it links, as the Setup
    /// stored them. A Setup of an Event that has started stays in the collection, frozen.
    /// </summary>
    public async Task<Guid> SetupAsync(
        string? tenant = "nts",
        Guid? mainOperator = null,
        IEnumerable<(string Email, string Role)>? officials = null,
        IEnumerable<string>? operators = null,
        Guid? id = null
    )
    {
        id ??= Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Uuid(id.Value) },
            { "Name", "Setup " + id.Value.ToString("N")[..6] },
            { "Location", "Sofia" },
            { "Competitions", new BsonArray() },
            {
                "Officials",
                new BsonArray(
                    (officials ?? []).Select(x => new BsonDocument
                    {
                        { "Id", Uuid(Guid.NewGuid()) },
                        { "Name", "Official " + x.Email },
                        { "Role", x.Role },
                        {
                            "User",
                            new BsonDocument
                            {
                                { "Id", Uuid(Guid.NewGuid()) },
                                { "TenantId", "nts" },
                                { "Email", x.Email },
                            }
                        },
                    })
                )
            },
            {
                "Operators",
                new BsonArray(
                    (operators ?? []).Select(x => new BsonDocument
                    {
                        { "Id", Uuid(Guid.NewGuid()) },
                        {
                            "User",
                            new BsonDocument
                            {
                                { "Id", Uuid(Guid.NewGuid()) },
                                { "TenantId", "nts" },
                                { "Email", x },
                            }
                        },
                        { "Role", "Steward" },
                    })
                )
            },
        };
        if (tenant != null)
        {
            document["TenantId"] = tenant;
        }

        if (mainOperator != null)
        {
            document["MainOperatorId"] = Uuid(mainOperator.Value);
        }

        await Collection("configure_events").InsertOneAsync(document);
        return id.Value;
    }

    /// <summary>The Core document of an Event that has started: it ends at the instant given, as a date.</summary>
    public async Task StartedAsync(Guid id, DateTimeOffset end, string? tenant = "nts", Guid? mainOperator = null)
    {
        var document = new BsonDocument
        {
            { "_id", Uuid(id) },
            { "Name", "Event " + id.ToString("N")[..6] },
            { "Location", "Sofia" },
            { "StartDay", end.UtcDateTime.AddDays(-2) },
            { "EndDay", end.UtcDateTime },
        };
        if (tenant != null)
        {
            document["TenantId"] = tenant;
        }

        if (mainOperator != null)
        {
            document["MainOperatorId"] = Uuid(mainOperator.Value);
        }

        await Collection("event_informations").InsertOneAsync(document);
    }

    /// <summary>The copy of an Official an Event made when it started: it names the user it is linked to by id.</summary>
    public async Task<Guid> OfficialCopyAsync(Guid eventId, Guid? userId, string role, bool deleted = false)
    {
        return await OwnedAsync(
            "event_officials",
            "nts",
            document =>
            {
                document["EventId"] = Uuid(eventId);
                document["Role"] = role;
                if (userId != null)
                {
                    document["UserId"] = Uuid(userId.Value);
                }

                document["IsDeleted"] = deleted;
            }
        );
    }

    public async Task<Guid> OperatorCopyAsync(Guid eventId, Guid userId, bool deleted = false)
    {
        return await OwnedAsync(
            "event_operators",
            "nts",
            document =>
            {
                document["EventId"] = Uuid(eventId);
                document["UserId"] = Uuid(userId);
                document["Role"] = "Steward";
                document["IsDeleted"] = deleted;
            }
        );
    }

    /// <summary>The state a person kept for an Event, keyed by what identified the person then.</summary>
    public async Task<Guid> SessionAsync(string userIdentifier, Guid eventId)
    {
        var id = Guid.NewGuid();
        await Collection("event_user_sessions")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", ObjectId.GenerateNewId() },
                    { "Id", Uuid(id) },
                    { "TenantId", "nts" },
                    { "EventId", Uuid(eventId) },
                    { "UserIdentifier", userIdentifier },
                    {
                        "State",
                        new BsonDocument
                        {
                            { "SnapshotHistory", new BsonArray() },
                            { "SnapshotSelections", new BsonArray() },
                        }
                    },
                }
            );
        return id;
    }

    public async Task SettingsAsync(int documents)
    {
        for (var i = 0; i < documents; i++)
        {
            await Collection("settings")
                .InsertOneAsync(new BsonDocument { { "_id", Uuid(Guid.NewGuid()) }, { "OnlyAverageLoopSpeed", true } });
        }
    }

    /// <summary>Makes an account a Tenant Root of a Tenant, as the Developer's command does.</summary>
    public async Task TenantRootAsync(Guid account, string tenant = BULGARIA_ID)
    {
        await Collection("users")
            .UpdateOneAsync(
                new BsonDocument("_id", Uuid(account)),
                Builders<BsonDocument>.Update.Set(
                    "Memberships",
                    new BsonArray
                    {
                        new BsonDocument
                        {
                            { "TenantId", tenant },
                            {
                                "Roles",
                                new BsonArray { "tenant-root" }
                            },
                        },
                    }
                )
            );
    }

    /// <summary>Every document of every collection, as text, so that a test can tell that a run changed nothing.</summary>
    public async Task<SortedDictionary<string, string[]>> AllAsync()
    {
        var all = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var name in await (await Database.ListCollectionNamesAsync()).ToListAsync())
        {
            var documents = await Collection(name).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            all[name] = [.. documents.Select(x => x.ToJson()).Order(StringComparer.Ordinal)];
        }

        return all;
    }

    public async Task<BsonDocument> OneAsync(string collection, Guid id)
    {
        return await Collection(collection).Find(new BsonDocument("_id", Uuid(id))).SingleAsync();
    }

    public async Task<List<BsonDocument>> ManyAsync(string collection)
    {
        return await Collection(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    }
}
