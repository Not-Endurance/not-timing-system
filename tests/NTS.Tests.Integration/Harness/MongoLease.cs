using MongoDB.Driver;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// A database of a class in the MongoDB of the test run (ADR-0016). One container serves every class of the run, and each
/// class has a database of its own, which the lease drops when the class ends. A lease holds one of four places, so four
/// classes run at a time: that is what the localhost can start at once, and it is the limit here because the Visual Studio
/// adapter does not apply the thread limit of xUnit.
/// </summary>
internal sealed class MongoLease : IAsyncDisposable
{
    static readonly SemaphoreSlim PLACES = new(4, 4);
    static readonly Lazy<Task<MongoContainer>> RUN = new(() => MongoContainer.StartAsync());

    static MongoLease()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseRun();
    }

    readonly MongoContainer _run;
    readonly string _database;
    readonly string _connectionString;

    MongoLease(MongoContainer run, string database)
    {
        _run = run;
        _database = database;
        // The users of the container are in the admin database. Without the source named, a connection string that names a
        // database would make MongoDB look for them in that one, and the sign-in of the driver would fail.
        _connectionString = new MongoUrlBuilder(run.ConnectionString)
        {
            DatabaseName = database,
            AuthenticationSource = MongoUrl.Create(run.ConnectionString).AuthenticationSource ?? "admin",
        }.ToString();
    }

    /// <summary>The connection string of the class's database: the run's MongoDB, with the database named in it.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>Takes a place and a database of the run's MongoDB for the class. The class gives both back by disposing it.</summary>
    public static async Task<MongoLease> StartAsync()
    {
        await PLACES.WaitAsync();
        try
        {
            var run = await RUN.Value;
            return new MongoLease(run, "nts_" + Guid.NewGuid().ToString("N"));
        }
        catch
        {
            PLACES.Release();
            throw;
        }
    }

    /// <summary>The database a connection string names, which a test reads and writes its documents in.</summary>
    public static string DatabaseNameOf(string connectionString)
    {
        return MongoUrl.Create(connectionString).DatabaseName
            ?? throw new InvalidOperationException("The connection string names no database.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await new MongoClient(_run.ConnectionString).DropDatabaseAsync(_database);
        }
        finally
        {
            PLACES.Release();
        }
    }

    /// <summary>Removes the run's container and its volume when the test process ends, as the run is over then.</summary>
    static void ReleaseRun()
    {
        if (RUN.IsValueCreated && RUN.Value.IsCompletedSuccessfully)
        {
            RUN.Value.Result.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
