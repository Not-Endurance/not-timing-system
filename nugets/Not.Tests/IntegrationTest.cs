using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor.Ports;
using Not.Localization;
using Not.Startup;
using Not.Tests.RPC;
using Xunit;
using Xunit.Abstractions;

namespace Not.Tests;

public abstract class IntegrationTest : IDisposable
{
    const string SEED_DIRECTORY = "Seeds";
    const string ID_PATTERN = @"(?:^\s+|,\s+)""Id"": [0-9]+";
    static readonly TimeSpan RPC_DELAY = TimeSpan.FromSeconds(5);
    static readonly Regex ID_REGEX = new(ID_PATTERN);
    static readonly SemaphoreSlim SEMAPHORE = new(1);

    readonly ITestOutputHelper _testOutputHelper;
    string _stateName;
    string _storageFilePath;
    string _storageDirectory;
    string _testClassName;

    protected IntegrationTest(string stateName, ITestOutputHelper testOutputHelper)
    {
        var executionDirectory = Directory.GetCurrentDirectory();
        var tempDirectory = Guid.NewGuid().ToString();

        _stateName = stateName;
        _testOutputHelper = testOutputHelper;
        _storageDirectory = Path.Combine(executionDirectory, tempDirectory);
        _storageFilePath = $"{_storageDirectory}/{stateName}.json";
        _testClassName = GetType().Name;

        Directory.CreateDirectory(_storageDirectory);

        var services = ConfigureServices(_storageDirectory);
        services.AddDummyLocalizer();
        Provider = services.BuildServiceProvider();

        foreach (var initializer in Provider.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }
    }

    protected abstract IServiceCollection ConfigureServices(string storagePath);

    protected IServiceProvider Provider { get; private set; }

    protected async Task<T> GetBehind<T>(Action<string> log)
        where T : notnull
    {
        var behind = Provider.GetRequiredService<T>();
        if (behind is IStatefulService observableBehind)
        {
            await observableBehind.Initialize([log]);
        }
        return behind;
    }

    /// <summary>
    /// Seeds the test store with predefined data. <br />
    /// The Data is expected at the following path: {state-name}/{test-class-name}/{test-class-name | test-method-name}.json <br/>
    /// If both files are present {test-method-name} takes precedence
    /// </summary>
    /// <param name="test"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected async Task Seed([CallerMemberName] string? test = null)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var path = Path.Combine(currentDirectory, SEED_DIRECTORY, _stateName, _testClassName, $"{test}.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(currentDirectory, SEED_DIRECTORY, _stateName, _testClassName, $"{_testClassName}.json");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Seed file '{test}.json' or '{_testClassName}.json' not found");
            }
        }
        var contents = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(_storageFilePath, contents);
    }

    protected async Task AssertStateEquals(string expectedContents)
    {
        var contents = await File.ReadAllTextAsync(_storageFilePath);
        var expected = Normalize(expectedContents);
        var actual = Normalize(contents);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
    }

    public async Task AssertRpcInvoked<T>(HubFixture<T> fixture, Func<Task> action, string rpcName)
        where T : ITestRpcClient
    {
        try
        {
            await SEMAPHORE.WaitAsync();

            var hubProcess = fixture.HubProcess;
            using var client = fixture.GetClient(_testOutputHelper);
            await client.Connect();
            await action();
            await Task.Delay(RPC_DELAY); //TODO: a more sophisticated method maybe necessary with a lot of tests
            Assert.Contains(rpcName, client.InvokedMethods, EqualityComparer<string>.Default);
        }
        finally
        {
            SEMAPHORE.Release();
        }
    }

    public void Dispose()
    {
        File.Delete(_storageFilePath);
        Directory.Delete(_storageDirectory);
    }

    string Normalize(string contents)
    {
        return ID_REGEX.Replace(contents, string.Empty);
    }
}
