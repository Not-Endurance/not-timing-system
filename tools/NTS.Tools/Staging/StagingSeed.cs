using System.Net.Mail;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Not.Storage;
using Not.Storage.Mongo;
using NTS.Application.Factories;
using NTS.Application.Mongo;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Services.StartValidation;
using static NTS.Tools.Shared.Records;

namespace NTS.Tools.Staging;

/// <summary>
/// <c>seed-staging</c> (#607): makes, in the Tenant of Bulgaria, what a person who tries the platform on a staging database
/// needs: a Tenant Root (which makes the Tenant operational), a Main Operator, Officials and Operators, and a Live Event with
/// twelve Participations that the Officials may send Snapshots to. The accounts are the ones named by their email, made when
/// they are not there and otherwise left as they are but for what the seed gives them (a membership of the Tenant, the role of
/// Tenant Root); the person signs in with a code sent to the address, which confirms it. The Event is made the way the Api
/// starts one: its Setup, the Core document, and what the Event copies and creates, the documents of which come from the same
/// code (<see cref="EventStartDocuments"/>). It refuses a database whose marker says Production and one that has no marker
/// unless it is told which environment it is, and never marks a database as Production. Everything it makes is made once: it
/// can be run again, and a run that stopped is finished by running it again.
/// </summary>
public static class StagingSeed
{
    const string BULGARIA_ISO = "BG";
    const string SETUPS = "configure_events";
    const string CORES = "event_informations";
    const string GRANTS = "event_grants";
    const string USERS = "users";
    const string TENANTS = "tenants";
    const string COUNTRIES = "countries";
    const string OFFICIALS = "event_officials";
    const string OPERATORS = "event_operators";
    const string PARTICIPATIONS = "event_participations";
    const string RANKINGS = "event_rankings";
    static readonly object CONVENTIONS = new();

    public static async Task<StagingSeedReport> Run(
        IMongoDatabase database,
        StagingSeedOptions options,
        DateTimeOffset now
    )
    {
        Configure();
        var run = new Seeding(database, options, now);
        await run.ReadAsync();
        if (options.Apply && !run.Report.Refused)
        {
            await run.WriteAsync();
            run.Report.Applied = true;
        }

        return run.Report;
    }

    /// <summary>How the models are written as documents: the way the Api maps them, so that what is seeded is read as it is.</summary>
    static void Configure()
    {
        NStorageBuilder.RegisterSerializers();
        NtsMongoSerialization.Configure();
        lock (CONVENTIONS)
        {
            if (
                !ConventionRegistry
                    .Lookup(typeof(ConfigureEventModel))
                    .Conventions.Any(x => x is EnumRepresentationConvention)
            )
            {
                ConventionRegistry.Register(
                    "NTS models of the seed",
                    new ConventionPack
                    {
                        new IgnoreExtraElementsConvention(true),
                        new EnumRepresentationConvention(BsonType.String),
                    },
                    type => type.FullName?.StartsWith("NTS.", StringComparison.Ordinal) == true
                );
            }
        }
    }

    sealed class Seeding
    {
        readonly IMongoDatabase _database;
        readonly StagingSeedOptions _options;
        readonly DateTimeOffset _now;
        readonly List<Person> _people = [];
        readonly List<BsonDocument> _newAccounts = [];
        readonly List<UpdateOneModel<BsonDocument>> _accountUpdates = [];
        readonly List<(string Collection, IReadOnlyList<BsonDocument> Documents)> _children = [];
        readonly List<BsonDocument> _grants = [];
        BsonDocument? _country;
        BsonDocument? _tenant;
        BsonDocument? _tenantCompletion;
        BsonDocument? _setup;
        BsonDocument? _core;
        bool _rootMade;

        public Seeding(IMongoDatabase database, StagingSeedOptions options, DateTimeOffset now)
        {
            _database = database;
            _options = options;
            _now = now;
            Report = new StagingSeedReport(options.Apply);
        }

        public StagingSeedReport Report { get; }

        public async Task ReadAsync()
        {
            await ReadMarkerAsync();
            ValidateAsked();
            if (Report.Refusals.Count > 0)
            {
                return;
            }

            var country = await ReadCountryAsync();
            await ReadTenantAsync();
            await ReadAccountsAsync();
            await ReadEventAsync(country);
        }

        public async Task WriteAsync()
        {
            if (Report.MarkWith != null)
            {
                await EnvironmentMarker.WriteAsync(_database, Report.MarkWith, _now);
            }

            if (_country != null)
            {
                await Collection(COUNTRIES).InsertOneAsync(_country);
            }

            if (_tenant != null)
            {
                await Collection(TENANTS)
                    .UpdateOneAsync(
                        new BsonDocument("_id", StagingDataset.TENANT_ID),
                        new BsonDocument("$setOnInsert", new BsonDocument(_tenant.Where(x => x.Name != "_id"))),
                        new UpdateOptions { IsUpsert = true }
                    );
            }

            if (_tenantCompletion != null)
            {
                await Collection(TENANTS)
                    .UpdateOneAsync(
                        new BsonDocument("_id", StagingDataset.TENANT_ID),
                        new BsonDocument("$set", _tenantCompletion)
                    );
            }

            if (_newAccounts.Count > 0)
            {
                await Collection(USERS).InsertManyAsync(_newAccounts);
            }

            foreach (var update in _accountUpdates)
            {
                await Collection(USERS).BulkWriteAsync([update]);
            }

            if (_setup != null)
            {
                await Collection(SETUPS).InsertOneAsync(_setup);
            }

            if (_core != null)
            {
                await Collection(CORES).InsertOneAsync(_core);
            }

            foreach (var (collection, documents) in _children)
            {
                await Collection(collection).InsertManyAsync(documents);
            }

            if (_grants.Count > 0)
            {
                await Collection(GRANTS).InsertManyAsync(_grants);
            }
        }

        async Task ReadMarkerAsync()
        {
            Report.MarkedAs = await EnvironmentMarker.ReadAsync(_database);
            var named = EnvironmentMarker.Canonical(_options.Environment);
            if (!string.IsNullOrWhiteSpace(_options.Environment) && named == null)
            {
                Report.Refuse($"'{_options.Environment}' is not an environment: use Staging or Development.");
            }

            if (EnvironmentMarker.IsProduction(Report.MarkedAs) || EnvironmentMarker.IsProduction(named))
            {
                Report.Refuse("The database is marked Production, and nothing is seeded into production.");
                return;
            }

            if (Report.MarkedAs != null)
            {
                if (named != null && !string.Equals(named, Report.MarkedAs, StringComparison.Ordinal))
                {
                    Report.Refuse(
                        $"The database is marked {Report.MarkedAs}, not {named}: a marker is not changed to another name."
                    );
                }

                return;
            }

            if (named == null)
            {
                Report.Refuse(
                    "The database has no marker: say which environment it is with --environment Staging or Development, which marks it."
                );
                return;
            }

            Report.MarkWith = named;
        }

        void ValidateAsked()
        {
            foreach (
                var (what, email) in new[]
                {
                    ("--tenant-root", _options.TenantRoot),
                    ("--main-operator", _options.MainOperator),
                }
            )
            {
                if (!IsEmail(email))
                {
                    Report.Refuse($"{what} needs the email of an account.");
                }
            }

            if (_options.Days is < 1 or > 60)
            {
                Report.Refuse("--days is from 1 to 60.");
            }

            if (_options.StartTime < TimeSpan.Zero || _options.StartTime >= TimeSpan.FromHours(24))
            {
                Report.Refuse("--start is a time of the day.");
            }

            if (string.IsNullOrWhiteSpace(_options.EventName))
            {
                Report.Refuse("--event-name is not empty.");
            }

            foreach (var email in _options.Officials.Select(x => x.Email).Concat(_options.Operators))
            {
                if (!IsEmail(email))
                {
                    Report.Refuse("--official and --operator need the email of an account.");
                    break;
                }
            }
        }

        async Task<Country> ReadCountryAsync()
        {
            var documents = await Collection(COUNTRIES).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            foreach (var document in documents)
            {
                if (
                    string.Equals(TextOf(document, "IsoCode"), BULGARIA_ISO, StringComparison.OrdinalIgnoreCase)
                    && TextOf(document, "Name") is { } name
                    && GuidOf(document, "_id") is { } id
                )
                {
                    return new Country(id, name, BULGARIA_ISO, TextOf(document, "NfCode"), TextOf(document, "Locale"));
                }
            }

            var made = new Country(StagingDataset.IdOf("country:BG"), "Bulgaria", BULGARIA_ISO, "BUL", "bg-BG");
            _country = new BsonDocument
            {
                { "_id", Uuid(made.Id) },
                { TENANT_ID, Tenant.LEGACY_ID },
                { "Name", made.Name },
                { "IsoCode", made.IsoCode },
                { "NfCode", made.NfCode },
                { "Locale", made.Locale },
            };
            Report.CountryMade = true;
            return made;
        }

        async Task ReadTenantAsync()
        {
            var existing = await Collection(TENANTS)
                .Find(new BsonDocument("_id", StagingDataset.TENANT_ID))
                .FirstOrDefaultAsync();
            var rules = new BsonDocument("OnlyAverageLoopSpeed", true);
            if (existing == null)
            {
                _tenant = new BsonDocument
                {
                    { "_id", StagingDataset.TENANT_ID },
                    { "Name", "Bulgaria" },
                    { "Kind", Tenant.COUNTRY },
                    { "RegionalRules", rules },
                };
                Report.TenantMade = true;
                return;
            }

            var missing = new BsonDocument();
            if (!HasValue(existing, "Name"))
            {
                missing["Name"] = "Bulgaria";
            }

            if (!HasValue(existing, "Kind"))
            {
                missing["Kind"] = Tenant.COUNTRY;
            }

            if (!existing.Contains("RegionalRules"))
            {
                missing["RegionalRules"] = rules;
            }

            if (missing.ElementCount > 0)
            {
                _tenantCompletion = missing;
                Report.TenantCompleted = true;
            }
        }

        async Task ReadAccountsAsync()
        {
            var emails = new List<(string Email, bool Root)> { (NormalEmail(_options.TenantRoot)!, true) };
            emails.Add((NormalEmail(_options.MainOperator)!, false));
            emails.AddRange(_options.Officials.Select(x => (NormalEmail(x.Email)!, false)));
            emails.AddRange(_options.Operators.Select(x => (NormalEmail(x)!, false)));
            var made = new List<Guid>();
            var kept = 0;
            foreach (var group in emails.GroupBy(x => x.Email))
            {
                var isRoot = group.Any(x => x.Root);
                var existing = await Collection(USERS)
                    .Find(
                        new BsonDocument("Email", group.Key),
                        new FindOptions { Collation = new Collation("en", strength: CollationStrength.Secondary) }
                    )
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    var id = Guid.NewGuid();
                    made.Add(id);
                    _people.Add(new Person(group.Key, id));
                    _newAccounts.Add(NewAccount(id, group.Key, isRoot));
                    _rootMade |= isRoot;
                    continue;
                }

                kept++;
                var accountId = GuidOf(existing, "_id");
                if (accountId == null)
                {
                    Report.Refuse(
                        "An account that is named is not keyed by a standard UUID, which Identity cannot read: convert the ids first."
                    );
                    continue;
                }

                _people.Add(new Person(group.Key, accountId.Value));
                PlanForExistingAccount(existing, accountId.Value, isRoot);
            }

            Report.AccountsMade = made;
            Report.AccountsKept = kept;
            Report.TenantRootMade = _rootMade;
        }

        BsonDocument NewAccount(Guid id, string email, bool root)
        {
            return new BsonDocument
            {
                { "_id", Uuid(id) },
                { "Email", email },
                { "EmailConfirmed", false },
                { "SecurityStamp", Guid.NewGuid().ToString("N") },
                { "LockoutEnabled", false },
                { "AccessFailedCount", 0 },
                { "Roles", new BsonArray() },
                { "CountryRegion", "Bulgaria" },
                { HOME_TENANT, StagingDataset.TENANT_ID },
                {
                    MEMBERSHIPS,
                    new BsonArray
                    {
                        root
                            ? NewMembership(StagingDataset.TENANT_ID, Membership.TENANT_ROOT)
                            : NewMembership(StagingDataset.TENANT_ID),
                    }
                },
            };
        }

        /// <summary>An account that is there keeps everything it has: it gets a home Tenant it lacks, the membership it lacks, and the role of Tenant Root when it is to have it.</summary>
        void PlanForExistingAccount(BsonDocument account, Guid id, bool root)
        {
            var filter = new BsonDocument("_id", Uuid(id));
            var set = Builders<BsonDocument>.Update;
            if (!HasValue(account, HOME_TENANT))
            {
                _accountUpdates.Add(
                    new UpdateOneModel<BsonDocument>(filter, set.Set(HOME_TENANT, StagingDataset.TENANT_ID))
                );
            }

            var membership =
                account.TryGetValue(MEMBERSHIPS, out var memberships) && memberships.IsBsonArray
                    ? memberships
                        .AsBsonArray.Where(x => x.IsBsonDocument)
                        .Select(x => x.AsBsonDocument)
                        .FirstOrDefault(x => x.GetValue(TENANT_ID, BsonNull.Value) == StagingDataset.TENANT_ID)
                    : null;
            if (membership == null)
            {
                _accountUpdates.Add(
                    new UpdateOneModel<BsonDocument>(
                        filter,
                        account.TryGetValue(MEMBERSHIPS, out var existing) && existing.IsBsonArray
                            ? set.Push(
                                MEMBERSHIPS,
                                root
                                    ? NewMembership(StagingDataset.TENANT_ID, Membership.TENANT_ROOT)
                                    : NewMembership(StagingDataset.TENANT_ID)
                            )
                            : set.Set(
                                MEMBERSHIPS,
                                new BsonArray
                                {
                                    root
                                        ? NewMembership(StagingDataset.TENANT_ID, Membership.TENANT_ROOT)
                                        : NewMembership(StagingDataset.TENANT_ID),
                                }
                            )
                    )
                );
                _rootMade |= root;
            }
            else if (
                root
                && !(
                    membership.TryGetValue("Roles", out var roles)
                    && roles.IsBsonArray
                    && roles.AsBsonArray.Contains(Membership.TENANT_ROOT)
                )
            )
            {
                _accountUpdates.Add(
                    new UpdateOneModel<BsonDocument>(
                        new BsonDocument
                        {
                            { "_id", Uuid(id) },
                            { $"{MEMBERSHIPS}.{TENANT_ID}", StagingDataset.TENANT_ID },
                        },
                        set.AddToSet($"{MEMBERSHIPS}.$.Roles", Membership.TENANT_ROOT)
                    )
                );
                _rootMade = true;
            }
        }

        async Task ReadEventAsync(Country country)
        {
            var mainOperator = _people.First(x => x.Email == NormalEmail(_options.MainOperator));
            SeededPerson Seeded(string email, OfficialRole role)
            {
                return new SeededPerson(
                    NormalEmail(email)!,
                    _people.First(x => x.Email == NormalEmail(email)).Id,
                    role
                );
            }

            var officials = _options.Officials.Select(x => Seeded(x.Email, x.Role)).ToList();
            var operators = _options.Operators.Select(x => Seeded(x, OfficialRole.Steward)).ToList();
            var start = new DateTimeOffset(_now.UtcDateTime.Date, TimeSpan.Zero) + _options.StartTime;
            var setup = StagingDataset.Create(
                _options.EventName.Trim(),
                start,
                country,
                StagingDataset.TENANT_ID,
                mainOperator.Id,
                officials,
                operators
            );
            Report.EventId = setup.Id;
            var issues = StartValidator.Validate(setup).Data ?? [];
            if (issues.Count > 0 || FeiExportConfiguration.MissingOf(setup).Count > 0)
            {
                throw new InvalidOperationException(
                    "The Setup of the seed is not whole: it is a defect of the seed, not of the database."
                );
            }

            var documents = EventStartDocuments.From(setup, new RegionalRules(true));
            var end = new DateTimeOffset(_now.UtcDateTime.Date.AddDays(_options.Days - 1), TimeSpan.Zero)
                .AddHours(23)
                .AddMinutes(59)
                .AddSeconds(59);
            documents.EventInformation.StartDay = new DateTimeOffset(_now.UtcDateTime.Date, TimeSpan.Zero);
            documents.EventInformation.EndDay = end;
            Report.EventEnds = end;

            if (await Collection(SETUPS).CountDocumentsAsync(new BsonDocument("_id", Uuid(setup.Id))) == 0)
            {
                _setup = ConfigureEventModel.From(setup).ToBsonDocument();
                Report.SetupMade = true;
            }

            if (await Collection(CORES).CountDocumentsAsync(new BsonDocument("_id", Uuid(setup.Id))) == 0)
            {
                _core = documents.EventInformation.ToBsonDocument();
                Report.CoreMade = true;
            }

            var counts = new Dictionary<string, int>();
            foreach (
                var (collection, made) in new (string, IReadOnlyList<BsonDocument>)[]
                {
                    (OFFICIALS, documents.Officials),
                    (OPERATORS, documents.Operators),
                    (PARTICIPATIONS, documents.Participations),
                    (RANKINGS, documents.Rankings),
                }
            )
            {
                if (
                    made.Count > 0
                    && await Collection(collection).CountDocumentsAsync(new BsonDocument("EventId", Uuid(setup.Id)))
                        == 0
                )
                {
                    _children.Add((collection, [.. made.Select(x => Stamped(x))]));
                    counts[collection] = made.Count;
                }
            }

            Report.EventDocuments = counts;
            await ReadGrantsAsync(setup.Id, officials, operators);
        }

        async Task ReadGrantsAsync(
            Guid eventId,
            IReadOnlyList<SeededPerson> officials,
            IReadOnlyList<SeededPerson> operators
        )
        {
            var existing = new HashSet<string>();
            foreach (
                var grant in await Collection(GRANTS).Find(new BsonDocument("EventId", Uuid(eventId))).ToListAsync()
            )
            {
                existing.Add(GrantKeyOf(grant));
            }

            foreach (var official in officials)
            {
                Want(
                    EventGrant.ForOfficial(
                        Guid.NewGuid(),
                        eventId,
                        StagingDataset.TENANT_ID,
                        official.Role,
                        official.Email,
                        official.AccountId
                    ),
                    existing
                );
            }

            foreach (var operator_ in operators)
            {
                Want(
                    EventGrant.ForOperator(
                        Guid.NewGuid(),
                        eventId,
                        StagingDataset.TENANT_ID,
                        operator_.Email,
                        operator_.AccountId
                    ),
                    existing
                );
            }

            Report.GrantsMade = _grants.Count;
        }

        void Want(EventGrant grant, HashSet<string> existing)
        {
            var document = GrantDocument(grant);
            if (existing.Add(GrantKeyOf(document)))
            {
                _grants.Add(document);
            }
        }

        static BsonDocument Stamped(BsonDocument document)
        {
            var stamped = document.DeepClone().AsBsonDocument;
            stamped[TENANT_ID] = StagingDataset.TENANT_ID;
            return stamped;
        }

        static bool IsEmail(string? email)
        {
            return !string.IsNullOrWhiteSpace(email)
                && MailAddress.TryCreate(email.Trim(), out var address)
                && address.Address == email.Trim();
        }

        IMongoCollection<BsonDocument> Collection(string name)
        {
            return _database.GetCollection<BsonDocument>(name);
        }
    }

    sealed class Person
    {
        public Person(string email, Guid id)
        {
            Email = email;
            Id = id;
        }

        public string Email { get; }
        public Guid Id { get; }
    }
}
