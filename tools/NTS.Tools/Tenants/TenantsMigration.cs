using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using static NTS.Tools.Shared.Records;

namespace NTS.Tools.Tenants;

/// <summary>
/// <c>migrate-tenants</c> (#607, ADR-0012): the data of before Tenants becomes the data of Tenants. It reads everything it
/// needs first and refuses to apply, writing nothing, when it finds what it cannot decide (two accounts that share an email,
/// no Bulgaria among the countries, a database marked as another environment); then it marks the database with the
/// environment it is, completes the accounts with the fields of Identity (the address unconfirmed), places each in the Tenant
/// of the country of its profile, makes the Tenants, stamps every document a Tenant owns with the Tenant of Bulgaria, gives
/// every Event that is not yet Historic its Tenant Root as Main Operator, turns the Officials and Operators that were linked
/// by email into grants, finds the state a person kept under the account that person is, drops the settings and makes the
/// indexes. What a Tenant owns is every collection of <see cref="Owned"/>, the list of the Api (<c>TenantOwned</c>). It is a
/// dry run unless asked to apply, every step writes only what is not there, and so running it again changes nothing.
/// Documents are read and written as they are stored, so what the command does not know of them is kept.
/// </summary>
public static class TenantsMigration
{
    const string USERS = "users";
    const string COUNTRIES = "countries";
    const string TENANTS = "tenants";
    const string SETTINGS = "settings";
    const string SESSIONS = "event_user_sessions";
    const string SETUPS = "configure_events";
    const string CORES = "event_informations";
    const string OFFICIALS = "event_officials";
    const string OPERATORS = "event_operators";
    const string MAIN_OPERATOR = "MainOperatorId";
    const string BULGARIA_ISO = "BG";

    /// <param name="now">The instant an Event is told Live or Historic at: the clock of the machine unless a test gives one.</param>
    public static async Task<TenantsReport> Run(IMongoDatabase database, TenantsOptions options, DateTimeOffset now)
    {
        var run = new Migration(database, options, now);
        await run.ReadAsync();
        if (options.Apply && !run.Report.Refused)
        {
            await run.WriteAsync();
            run.Report.Applied = true;
        }

        return run.Report;
    }

    /// <summary>Every collection whose documents belong to a Tenant, which is what <c>TenantOwned</c> of the Api lists: they are stamped with the Tenant they are in.</summary>
    public static IReadOnlyList<string> Owned { get; } =
        [
            "athletes",
            "horses",
            "clubs",
            SETUPS,
            CORES,
            OFFICIALS,
            OPERATORS,
            "event_participations",
            "event_rankings",
            "event_handouts",
            TenantIndexes.GRANTS,
        ];

    /// <summary>The Officials a Setup links to a user by email, with their role.</summary>
    static IEnumerable<(string Email, OfficialRole Role)> LinkedOfficials(BsonDocument? setup)
    {
        foreach (var official in ElementsOf(setup, "Officials"))
        {
            if (EmailOfUser(official) is { } email && RoleOf(official.GetValue("Role", BsonNull.Value)) is { } role)
            {
                yield return (email, role);
            }
        }
    }

    static IEnumerable<string> LinkedOperators(BsonDocument? setup)
    {
        return ElementsOf(setup, "Operators").Select(EmailOfUser).OfType<string>();
    }

    static string? EmailOfUser(BsonDocument linked)
    {
        return
            linked.TryGetValue("User", out var user)
            && user.IsBsonDocument
            && NormalEmail(TextOf(user.AsBsonDocument, "Email", trim: false)) is { } email
            ? email
            : null;
    }

    static IEnumerable<BsonDocument> ElementsOf(BsonDocument? document, string field)
    {
        return document != null && document.TryGetValue(field, out var value) && value.IsBsonArray
            ? value.AsBsonArray.Where(x => x.IsBsonDocument).Select(x => x.AsBsonDocument)
            : [];
    }

    static OfficialRole? RoleOf(BsonValue role)
    {
        if (role.IsString && Enum.TryParse<OfficialRole>(role.AsString, out var named) && Enum.IsDefined(named))
        {
            return named;
        }

        return role.IsNumeric && Enum.IsDefined(typeof(OfficialRole), role.ToInt32())
            ? (OfficialRole)role.ToInt32()
            : null;
    }

    static Guid? MainOperatorOf(BsonDocument? document)
    {
        return document == null ? null : GuidOf(document, MAIN_OPERATOR);
    }

    sealed class Migration
    {
        readonly IMongoDatabase _database;
        readonly TenantsOptions _options;
        readonly DateTimeOffset _now;
        readonly List<Account> _accounts = [];
        readonly List<UpdateOneModel<BsonDocument>> _accountUpdates = [];
        readonly List<(string Id, BsonDocument Document)> _tenantsToMake = [];
        readonly List<(string Id, UpdateDefinition<BsonDocument> Update)> _tenantsToComplete = [];
        readonly Dictionary<string, FilterDefinition<BsonDocument>> _stamps = [];
        readonly List<(string Collection, BsonValue Document, Guid MainOperator)> _mainOperators = [];
        readonly List<BsonDocument> _grants = [];
        readonly List<UpdateOneModel<BsonDocument>> _sessionUpdates = [];
        readonly List<(string Collection, CreateIndexModel<BsonDocument> Index)> _indexes = [];
        string? _bulgaria;
        bool _settingsExist;

        public Migration(IMongoDatabase database, TenantsOptions options, DateTimeOffset now)
        {
            _database = database;
            _options = options;
            _now = now;
            Report = new TenantsReport(options.Apply);
        }

        public TenantsReport Report { get; }

        /// <summary>Reads what there is and decides what would be written, and what stops an apply. Nothing is written here.</summary>
        public async Task ReadAsync()
        {
            await ReadMarkerAsync();
            var countries = await ReadCountriesAsync();
            await ReadAccountsAsync(countries);
            await ReadStampsAsync();
            await ReadEventsAsync();
            await ReadSessionsAsync();
            await ReadSettingsAsync();
            await ReadIndexesAsync();
        }

        /// <summary>Writes it, in an order that leaves a run that stopped to be finished by running it again.</summary>
        public async Task WriteAsync()
        {
            await EnvironmentMarker.WriteAsync(_database, Report.MarkWith!, _now);
            await WriteTenantsAsync();
            await WriteAccountsAsync();
            await MakeIndexesAsync(x => x.Collection != TenantIndexes.GRANTS || x.Index.Options.Unique == true);
            foreach (var (collection, filter) in _stamps)
            {
                await Collection(collection)
                    .UpdateManyAsync(filter, Builders<BsonDocument>.Update.Set(TENANT_ID, _bulgaria));
            }

            foreach (var (collection, document, account) in _mainOperators)
            {
                await Collection(collection)
                    .UpdateOneAsync(
                        new BsonDocument { { "_id", document }, { MAIN_OPERATOR, new BsonDocument("$exists", false) } },
                        Builders<BsonDocument>.Update.Set(MAIN_OPERATOR, Uuid(account))
                    );
            }

            await WriteGrantsAsync();
            if (_sessionUpdates.Count > 0)
            {
                await Collection(SESSIONS).BulkWriteAsync(_sessionUpdates, new BulkWriteOptions { IsOrdered = false });
            }

            if (_settingsExist)
            {
                await _database.DropCollectionAsync(SETTINGS);
                Report.SettingsDropped = true;
            }

            await MakeIndexesAsync(_ => true);
        }

        async Task ReadMarkerAsync()
        {
            Report.MarkedAs = await EnvironmentMarker.ReadAsync(_database);
            Report.MarkWith = EnvironmentMarker.Canonical(_options.Environment);
            if (Report.MarkWith == null)
            {
                Report.Refuse(
                    string.IsNullOrWhiteSpace(_options.Environment)
                        ? "Say which environment the database is with --environment (Production, Staging or Development): the marker is how the commands that must not touch a production database tell it."
                        : $"'{_options.Environment}' is not an environment: use Production, Staging or Development."
                );
            }
            else if (
                Report.MarkedAs != null
                && !string.Equals(Report.MarkedAs, Report.MarkWith, StringComparison.Ordinal)
            )
            {
                Report.Refuse(
                    $"The database is marked {Report.MarkedAs}, and a marker is not changed to another name: remove the document of the environment collection by hand if it is wrong."
                );
            }
        }

        async Task<IReadOnlyList<Country>> ReadCountriesAsync()
        {
            var documents = await Collection(COUNTRIES).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var withoutIso = new List<Guid>();
            var usable = new List<Country>();
            foreach (var document in documents)
            {
                var iso = TextOf(document, "IsoCode");
                var name = TextOf(document, "Name");
                var id = GuidOf(document, "_id");
                if (iso == null)
                {
                    if (id != null)
                    {
                        withoutIso.Add(id.Value);
                    }

                    continue;
                }

                if (name != null && id != null)
                {
                    usable.Add(
                        new Country(id.Value, name, iso, TextOf(document, "NfCode"), TextOf(document, "Locale"))
                    );
                }
            }

            Report.Countries = documents.Count;
            Report.CountriesWithoutIsoCode = withoutIso;
            var bulgaria = usable.FirstOrDefault(x =>
                string.Equals(x.IsoCode, BULGARIA_ISO, StringComparison.OrdinalIgnoreCase)
            );
            Report.HasBulgaria = bulgaria != null;
            if (bulgaria == null)
            {
                Report.Refuse(
                    "The countries have no Bulgaria (ISO code BG), so the Tenant of Bulgaria cannot be made: add it to the countries and run again."
                );
            }
            else
            {
                _bulgaria = Tenant.ForCountry(bulgaria).Id;
            }

            // The order the profile screen and the Api find a country in: by name, the first that matches.
            return [.. usable.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        }

        async Task ReadAccountsAsync(IReadOnlyList<Country> countries)
        {
            var documents = await Collection(USERS).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            Report.Accounts = documents.Count;
            var withoutEmail = new List<Guid>();
            var otherIds = 0;
            foreach (var document in documents)
            {
                var id = GuidOf(document, "_id");
                if (id == null)
                {
                    otherIds++;
                    continue;
                }

                var email = NormalEmail(TextOf(document, "Email", trim: false));
                if (email == null)
                {
                    withoutEmail.Add(id.Value);
                    continue;
                }

                _accounts.Add(new Account(id.Value, email, document));
            }

            Report.AccountsWithoutEmail = withoutEmail;
            Report.AccountsWithOtherIds = otherIds;
            foreach (var group in _accounts.GroupBy(x => x.Email).Where(x => x.Count() > 1))
            {
                Report.AddDuplicates([.. group.Select(x => x.Id)]);
            }

            if (Report.DuplicateEmails.Count > 0)
            {
                Report.Refuse(
                    $"{Report.DuplicateEmails.Count} emails belong to more than one account, and one person cannot be told from another: resolve the accounts listed (the same email in any case, with or without spaces) and run again."
                );
            }

            var tenants = new Dictionary<string, Tenant>();
            var placed = 0;
            var normalized = 0;
            foreach (var account in _accounts)
            {
                var (update, derived, emailChanged) = AccountUpdate(account, countries);
                if (emailChanged)
                {
                    normalized++;
                }

                if (derived != null)
                {
                    tenants[derived.Id] = derived;
                    Report.Place(derived.Id);
                }
                else if (!HasValue(account.Document, HOME_TENANT))
                {
                    Report.Unplaced++;
                }

                if (update != null)
                {
                    placed++;
                    _accountUpdates.Add(
                        new UpdateOneModel<BsonDocument>(new BsonDocument("_id", Uuid(account.Id)), update)
                    );
                }
            }

            Report.AccountsCompleted = placed;
            Report.EmailsNormalized = normalized;
            await ReadTenantsAsync(countries, tenants);
        }

        /// <summary>What is written to an account, the Tenant it is placed in when it is placed now, and whether its email is written otherwise.</summary>
        (UpdateDefinition<BsonDocument>? Update, Tenant? Derived, bool EmailChanged) AccountUpdate(
            Account account,
            IReadOnlyList<Country> countries
        )
        {
            var document = account.Document;
            var updates = new List<UpdateDefinition<BsonDocument>>();
            var set = Builders<BsonDocument>.Update;
            var emailChanged = !string.Equals(
                TextOf(document, "Email", trim: false),
                account.Email,
                StringComparison.Ordinal
            );
            if (emailChanged)
            {
                updates.Add(set.Set("Email", account.Email));
            }

            if (!document.Contains("EmailConfirmed"))
            {
                updates.Add(set.Set("EmailConfirmed", false));
            }

            if (!HasValue(document, "SecurityStamp"))
            {
                updates.Add(set.Set("SecurityStamp", Guid.NewGuid().ToString("N")));
            }

            if (!document.Contains("LockoutEnabled"))
            {
                updates.Add(set.Set("LockoutEnabled", false));
            }

            if (!document.Contains("AccessFailedCount"))
            {
                updates.Add(set.Set("AccessFailedCount", 0));
            }

            Tenant? derived = null;
            var home = TextOf(document, HOME_TENANT);
            if (home == null)
            {
                derived = Tenant.DerivedFrom(TextOf(document, "CountryRegion"), countries);
                home = derived?.Id;
                if (derived != null)
                {
                    updates.Add(set.Set(HOME_TENANT, derived.Id));
                }
            }

            if (home != null && !HasMembership(document, home))
            {
                updates.Add(
                    document.TryGetValue(MEMBERSHIPS, out var memberships) && memberships.IsBsonArray
                        ? set.Push(MEMBERSHIPS, NewMembership(home))
                        : set.Set(MEMBERSHIPS, new BsonArray { NewMembership(home) })
                );
            }

            return (updates.Count == 0 ? null : set.Combine(updates), derived, emailChanged);
        }

        async Task ReadTenantsAsync(IReadOnlyList<Country> countries, Dictionary<string, Tenant> derived)
        {
            if (_bulgaria == null)
            {
                return;
            }

            var wanted = new Dictionary<string, Tenant>(derived);
            var bulgaria = countries.First(x => Tenant.ForCountry(x).Id == _bulgaria);
            wanted[_bulgaria] = Tenant.ForCountry(bulgaria);
            var existing = (
                await Collection(TENANTS).Find(Builders<BsonDocument>.Filter.In("_id", wanted.Keys)).ToListAsync()
            ).ToDictionary(x => x["_id"].AsString);
            var made = new List<string>();
            var completed = new List<string>();
            foreach (var (id, tenant) in wanted.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var rules = id == _bulgaria ? new BsonDocument("OnlyAverageLoopSpeed", true) : null;
                if (!existing.TryGetValue(id, out var document))
                {
                    var made1 = new BsonDocument
                    {
                        { "_id", id },
                        { "Name", tenant.Name },
                        { "Kind", tenant.Kind },
                    };
                    if (rules != null)
                    {
                        made1["RegionalRules"] = rules;
                    }

                    _tenantsToMake.Add((id, made1));
                    made.Add(id);
                    continue;
                }

                var missing = new List<UpdateDefinition<BsonDocument>>();
                var set = Builders<BsonDocument>.Update;
                if (!HasValue(document, "Name"))
                {
                    missing.Add(set.Set("Name", tenant.Name));
                }

                if (!HasValue(document, "Kind"))
                {
                    missing.Add(set.Set("Kind", tenant.Kind));
                }

                if (rules != null && !document.Contains("RegionalRules"))
                {
                    missing.Add(set.Set("RegionalRules", rules));
                }

                if (missing.Count > 0)
                {
                    _tenantsToComplete.Add((id, set.Combine(missing)));
                    completed.Add(id);
                }
            }

            Report.TenantsMade = made;
            Report.TenantsCompleted = completed;
        }

        async Task ReadStampsAsync()
        {
            var unstamped = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq(TENANT_ID, BsonNull.Value),
                Builders<BsonDocument>.Filter.Eq(TENANT_ID, ""),
                Builders<BsonDocument>.Filter.Eq(TENANT_ID, Tenant.LEGACY_ID)
            );
            foreach (var collection in Owned)
            {
                var count = await Collection(collection).CountDocumentsAsync(unstamped);
                Report.Stamp(collection, count);
                if (count > 0)
                {
                    _stamps[collection] = unstamped;
                }
            }
        }

        async Task ReadEventsAsync()
        {
            var setups = await Collection(SETUPS).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var cores = await Collection(CORES).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var coresById = cores.Where(x => GuidOf(x, "_id") != null).ToDictionary(x => GuidOf(x, "_id")!.Value);
            var setupsById = setups.Where(x => GuidOf(x, "_id") != null).ToDictionary(x => GuidOf(x, "_id")!.Value);
            Report.EventsWithOtherIds = setups.Count + cores.Count - setupsById.Count - coresById.Count;
            var withoutAnEnd = new List<Guid>();
            var live = new List<LiveEvent>();
            foreach (var id in setupsById.Keys.Union(coresById.Keys))
            {
                setupsById.TryGetValue(id, out var setup);
                coresById.TryGetValue(id, out var core);
                if (
                    core != null
                    && core.TryGetValue("IsDeleted", out var deleted)
                    && deleted.IsBoolean
                    && deleted.AsBoolean
                )
                {
                    continue;
                }

                DateTimeOffset? end = null;
                if (core != null)
                {
                    if (core.TryGetValue("EndDay", out var day) && day.IsValidDateTime)
                    {
                        end = new DateTimeOffset(day.ToUniversalTime(), TimeSpan.Zero);
                    }
                    else
                    {
                        withoutAnEnd.Add(id);
                    }
                }

                if (EventStageRule.Of(core != null, end, _now) == EventStage.Historic)
                {
                    continue;
                }

                var tenant = EffectiveTenant((core ?? setup)!);
                live.Add(new LiveEvent(id, tenant, setup, core));
            }

            Report.EventsWithoutAnEnd = withoutAnEnd;
            Report.EventsNotHistoric = live.Count;
            ReadMainOperators(live);
            await ReadGrantsAsync(live);
        }

        void ReadMainOperators(List<LiveEvent> live)
        {
            Guid? named = null;
            if (!string.IsNullOrWhiteSpace(_options.MainOperatorEmail))
            {
                var email = NormalEmail(_options.MainOperatorEmail)!;
                named = _accounts.FirstOrDefault(x => x.Email == email)?.Id;
                if (named == null)
                {
                    Report.Refuse(
                        "--main-operator names an email that no account has: the account has to exist, so that it can be signed in as."
                    );
                }
            }

            var waiting = new List<Guid>();
            var notes = new List<string>();
            var assigned = 0;
            foreach (var @event in live)
            {
                var documents = new[] { (SETUPS, @event.Setup), (CORES, @event.Core) }
                    .Where(x => x.Item2 != null && MainOperatorOf(x.Item2) == null)
                    .ToList();
                if (documents.Count == 0)
                {
                    continue;
                }

                // The one document that has a Main Operator says who it is on the other, and Core is the authority of a started Event.
                var account = MainOperatorOf(@event.Core) ?? MainOperatorOf(@event.Setup) ?? named;
                if (account == null)
                {
                    var roots = TenantRootsOf(@event.Tenant);
                    if (roots.Count != 1)
                    {
                        waiting.Add(@event.Id);
                        var note =
                            roots.Count == 0
                                ? $"{@event.Tenant} has no Tenant Root: seed one with seed-tenant-root and run again, or name the account with --main-operator."
                                : $"{@event.Tenant} has {roots.Count} Tenant Roots: name the account that becomes the Main Operator with --main-operator.";
                        if (!notes.Contains(note))
                        {
                            notes.Add(note);
                        }

                        continue;
                    }

                    account = roots[0];
                }

                foreach (var (collection, document) in documents)
                {
                    _mainOperators.Add((collection, document!["_id"], account.Value));
                }

                assigned++;
            }

            Report.MainOperatorsAssigned = assigned;
            Report.EventsWaitingForMainOperator = waiting;
            Report.MainOperatorNote = notes.Count == 0 ? null : string.Join(" ", notes);
        }

        IReadOnlyList<Guid> TenantRootsOf(string tenant)
        {
            return
            [
                .. _accounts
                    .Where(x =>
                        x.Document.TryGetValue(MEMBERSHIPS, out var memberships)
                        && memberships.IsBsonArray
                        && memberships.AsBsonArray.Any(m =>
                            m.IsBsonDocument
                            && m.AsBsonDocument.GetValue(TENANT_ID, BsonNull.Value) == tenant
                            && m.AsBsonDocument.TryGetValue("Roles", out var roles)
                            && roles.IsBsonArray
                            && roles.AsBsonArray.Contains(NTS.Domain.Aggregates.Membership.TENANT_ROOT)
                        )
                    )
                    .Select(x => x.Id),
            ];
        }

        async Task ReadGrantsAsync(List<LiveEvent> live)
        {
            var existing = new HashSet<string>();
            foreach (
                var document in await Collection(TenantIndexes.GRANTS)
                    .Find(FilterDefinition<BsonDocument>.Empty)
                    .ToListAsync()
            )
            {
                existing.Add(GrantKeyOf(document));
            }

            var byId = _accounts.ToDictionary(x => x.Id);
            var byEmail = _accounts.GroupBy(x => x.Email).ToDictionary(x => x.Key, x => x.First());
            var liveIds = live.ToDictionary(x => x.Id);
            var wanted = new Dictionary<string, BsonDocument>();
            var dangling = 0;

            void Want(LiveEvent @event, GrantKind kind, OfficialRole? role, string email, Guid? account)
            {
                var grant =
                    kind == GrantKind.Operator
                        ? EventGrant.ForOperator(Guid.NewGuid(), @event.Id, @event.Tenant, email, account)
                        : EventGrant.ForOfficial(Guid.NewGuid(), @event.Id, @event.Tenant, role!.Value, email, account);
                var document = GrantDocument(grant);
                var key = GrantKeyOf(document);
                if (!existing.Contains(key))
                {
                    wanted.TryAdd(key, document);
                }
            }

            foreach (var @event in live)
            {
                foreach (var (user, role) in LinkedOfficials(@event.Setup))
                {
                    Want(
                        @event,
                        GrantKind.Official,
                        role,
                        user,
                        byEmail.TryGetValue(NormalEmail(user)!, out var a) ? a.Id : null
                    );
                }

                foreach (var user in LinkedOperators(@event.Setup))
                {
                    Want(
                        @event,
                        GrantKind.Operator,
                        null,
                        user,
                        byEmail.TryGetValue(NormalEmail(user)!, out var a) ? a.Id : null
                    );
                }
            }

            foreach (
                var (collection, kind) in new[] { (OFFICIALS, GrantKind.Official), (OPERATORS, GrantKind.Operator) }
            )
            {
                var copies = await Collection(collection)
                    .Find(Builders<BsonDocument>.Filter.In("EventId", liveIds.Keys.Select(Uuid)))
                    .ToListAsync();
                foreach (var copy in copies)
                {
                    if (copy.TryGetValue("IsDeleted", out var deleted) && deleted.IsBoolean && deleted.AsBoolean)
                    {
                        continue;
                    }

                    if (GuidOf(copy, "EventId") is not { } eventId || !liveIds.TryGetValue(eventId, out var @event))
                    {
                        continue;
                    }

                    if (GuidOf(copy, "UserId") is not { } userId)
                    {
                        continue; // an Official that is not linked to anybody has nobody to give a grant to
                    }

                    if (!byId.TryGetValue(userId, out var account))
                    {
                        dangling++;
                        continue;
                    }

                    var role = kind == GrantKind.Official ? RoleOf(copy.GetValue("Role", BsonNull.Value)) : null;
                    if (kind == GrantKind.Official && role == null)
                    {
                        continue;
                    }

                    Want(@event, kind, role, account.Email, account.Id);
                }
            }

            Report.GrantsMade = wanted.Count;
            Report.GrantsPending = wanted.Values.Count(x => !x.Contains("AccountId"));
            Report.GrantsDangling = dangling;
            _grants.AddRange(wanted.Values);
        }

        async Task ReadSessionsAsync()
        {
            var byEmail = _accounts.GroupBy(x => x.Email).ToDictionary(x => x.Key, x => x.First().Id);
            var rekeyed = 0;
            var kept = 0;
            foreach (var record in await Collection(SESSIONS).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            {
                var owner = TextOf(record, "UserIdentifier", trim: false);
                var email = NormalEmail(owner);
                if (email != null && byEmail.TryGetValue(email, out var account))
                {
                    rekeyed++;
                    _sessionUpdates.Add(
                        new UpdateOneModel<BsonDocument>(
                            new BsonDocument("_id", record["_id"]),
                            Builders<BsonDocument>.Update.Set("UserIdentifier", account.ToString())
                        )
                    );
                }
                else
                {
                    kept++;
                }
            }

            Report.SessionsRekeyed = rekeyed;
            Report.SessionsKept = kept;
        }

        async Task ReadSettingsAsync()
        {
            _settingsExist = (await (await _database.ListCollectionNamesAsync()).ToListAsync()).Contains(SETTINGS);
            Report.SettingsDocuments = _settingsExist
                ? await Collection(SETTINGS).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)
                : 0;
        }

        async Task ReadIndexesAsync()
        {
            var made = new List<string>();
            foreach (var group in TenantIndexes.All.GroupBy(x => x.Collection))
            {
                var existing = await TenantIndexes.ExistingAsync(Collection(group.Key));
                foreach (var (collection, index) in group)
                {
                    if (!existing.Contains(index.Options.Name))
                    {
                        _indexes.Add((collection, index));
                        made.Add(index.Options.Name);
                    }
                }
            }

            Report.IndexesMade = [.. made.Distinct()];
        }

        async Task WriteTenantsAsync()
        {
            foreach (var (_, document) in _tenantsToMake)
            {
                await Collection(TENANTS)
                    .UpdateOneAsync(
                        new BsonDocument("_id", document["_id"]),
                        new BsonDocument("$setOnInsert", new BsonDocument(document.Where(x => x.Name != "_id"))),
                        new UpdateOptions { IsUpsert = true }
                    );
            }

            foreach (var (id, update) in _tenantsToComplete)
            {
                await Collection(TENANTS).UpdateOneAsync(new BsonDocument("_id", id), update);
            }
        }

        async Task WriteAccountsAsync()
        {
            foreach (var chunk in _accountUpdates.Chunk(500))
            {
                await Collection(USERS).BulkWriteAsync(chunk, new BulkWriteOptions { IsOrdered = false });
            }
        }

        async Task WriteGrantsAsync()
        {
            if (_grants.Count == 0)
            {
                return;
            }

            try
            {
                await Collection(TenantIndexes.GRANTS)
                    .InsertManyAsync(_grants, new InsertManyOptions { IsOrdered = false });
            }
            catch (MongoBulkWriteException ex)
                when (ex.WriteErrors.All(x => x.Category == ServerErrorCategory.DuplicateKey))
            {
                // The person was granted the same by a run that stopped, or by the Api, since this one looked.
            }
        }

        async Task MakeIndexesAsync(Func<(string Collection, CreateIndexModel<BsonDocument> Index), bool> which)
        {
            foreach (var entry in _indexes.Where(which).ToList())
            {
                await Collection(entry.Collection).Indexes.CreateOneAsync(entry.Index);
                _indexes.Remove(entry);
            }
        }

        IMongoCollection<BsonDocument> Collection(string name)
        {
            return _database.GetCollection<BsonDocument>(name);
        }

        string EffectiveTenant(BsonDocument document)
        {
            var tenant = TextOf(document, TENANT_ID);
            return tenant == null || tenant == Tenant.LEGACY_ID ? _bulgaria ?? "country-bg" : tenant;
        }
    }

    sealed class Account
    {
        public Account(Guid id, string email, BsonDocument document)
        {
            Id = id;
            Email = email;
            Document = document;
        }

        public Guid Id { get; }

        /// <summary>The email as accounts and grants are matched by it: without the spaces around it, in lower case.</summary>
        public string Email { get; }

        public BsonDocument Document { get; }
    }

    sealed class LiveEvent
    {
        public LiveEvent(Guid id, string tenant, BsonDocument? setup, BsonDocument? core)
        {
            Id = id;
            Tenant = tenant;
            Setup = setup;
            Core = core;
        }

        public Guid Id { get; }
        public string Tenant { get; }
        public BsonDocument? Setup { get; }
        public BsonDocument? Core { get; }
    }
}
