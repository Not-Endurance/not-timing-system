using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.NameMigration;

public static class NameMigrationTool
{
    const string DEFAULT_DATABASE = "nts";
    static readonly string[] Collections =
    [
        "athletes",
        "horses",
        "configure_events",
        "event_officials",
        "event_participations",
        "event_rankings",
        "event_handouts",
        "event_user_sessions",
        "event_pending_snapshots",
    ];

    public static async Task<int> Run(string[] args)
    {
        var options = Parse(args);
        if (options.ShowHelp)
        {
            ShowHelp();
            return options.IsValid ? 0 : 1;
        }

        var client = new MongoClient(options.ConnectionString);
        var database = client.GetDatabase(options.Database);

        Console.WriteLine(options.Apply ? "Applying name migration." : "Dry-run name migration.");
        Console.WriteLine($"Database: {options.Database}");

        var total = 0;
        foreach (var collectionName in Collections)
        {
            var changed = await MigrateCollection(database, collectionName, options.Apply);
            total += changed;
            Console.WriteLine($"{collectionName}: {changed}");
        }

        Console.WriteLine($"Total changed documents: {total}");
        return 0;
    }

    static async Task<int> MigrateCollection(IMongoDatabase database, string collectionName, bool apply)
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var changed = 0;
        var rootContext = GetRootContext(collectionName);

        foreach (var document in documents)
        {
            var migrated = (BsonDocument)document.DeepClone();
            if (!MigrateDocument(migrated, rootContext))
            {
                continue;
            }

            changed++;
            if (apply && migrated.TryGetValue("_id", out var id))
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
                await collection.ReplaceOneAsync(filter, migrated);
            }
        }

        return changed;
    }

    static string? GetRootContext(string collectionName)
    {
        return collectionName switch
        {
            "athletes" => "athlete",
            "horses" => "horse",
            "event_officials" => "official",
            _ => null,
        };
    }

    static bool MigrateDocument(BsonDocument document, string? context)
    {
        var changed = false;

        if (TryReadJoinedNames(document, out var joinedName))
        {
            if (!HasNonEmptyString(document, "Name"))
            {
                document["Name"] = joinedName;
            }

            document.Remove("Names");
            changed = true;
        }

        if (IsActorContext(context) && !HasNonEmptyString(document, "Name") && TryReadString(document, "NameEnglish", out var nameEnglish))
        {
            document["Name"] = nameEnglish;
            changed = true;
        }

        foreach (var element in document.ToArray())
        {
            changed |= MigrateValue(element.Value, ResolveContext(element.Name, context));
        }

        return changed;
    }

    static bool MigrateValue(BsonValue value, string? context)
    {
        if (value is BsonDocument childDocument)
        {
            return MigrateDocument(childDocument, context);
        }

        if (value is not BsonArray array)
        {
            return false;
        }

        var changed = false;
        foreach (var item in array)
        {
            changed |= MigrateValue(item, context);
        }

        return changed;
    }

    static string? ResolveContext(string propertyName, string? context)
    {
        return (propertyName, context) switch
        {
            ("Athlete", _) => "athlete",
            ("Horse", _) => "horse",
            ("Officials", _) => "official",
            ("SnapshotHistory", _) => "snapshotGroup",
            ("SnapshotGroups", _) => "snapshotGroup",
            ("Entries", "snapshotGroup") => "snapshot",
            _ => null,
        };
    }

    static bool IsActorContext(string? context)
    {
        return context is "athlete" or "horse" or "official" or "snapshot";
    }

    static bool TryReadJoinedNames(BsonDocument document, out string joinedName)
    {
        joinedName = string.Empty;
        if (!document.TryGetValue("Names", out var value))
        {
            return false;
        }

        if (value is BsonArray array)
        {
            var parts = array
                .Where(x => x.IsString)
                .Select(x => x.AsString)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            joinedName = string.Join(' ', parts);
            return !string.IsNullOrWhiteSpace(joinedName);
        }

        if (value.IsString && !string.IsNullOrWhiteSpace(value.AsString))
        {
            joinedName = value.AsString;
            return true;
        }

        return false;
    }

    static bool HasNonEmptyString(BsonDocument document, string propertyName)
    {
        return TryReadString(document, propertyName, out _);
    }

    static bool TryReadString(BsonDocument document, string propertyName, out string value)
    {
        value = string.Empty;
        if (!document.TryGetValue(propertyName, out var bsonValue) || !bsonValue.IsString)
        {
            return false;
        }

        value = bsonValue.AsString;
        return !string.IsNullOrWhiteSpace(value);
    }

    static Options Parse(string[] args)
    {
        var options = new Options { Database = DEFAULT_DATABASE };

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connection-string" when i + 1 < args.Length:
                    options.ConnectionString = args[++i];
                    break;
                case "--database" when i + 1 < args.Length:
                    options.Database = args[++i];
                    break;
                case "--apply":
                    options.Apply = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    options.ShowHelp = true;
                    options.IsValid = false;
                    Console.Error.WriteLine($"Unknown or incomplete option '{args[i]}'.");
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString) && !options.ShowHelp)
        {
            options.ShowHelp = true;
            options.IsValid = false;
            Console.Error.WriteLine("--connection-string is required.");
        }

        return options;
    }

    static void ShowHelp()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- migrate-names --connection-string <mongo> [--database nts] [--apply]

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --database <name>             Database name. Defaults to nts.
              --apply                       Persist changes. Omit for dry-run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
