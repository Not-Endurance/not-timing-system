using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Tests;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public abstract class JudgeIntegrationTest : IntegrationTest
{
    protected JudgeIntegrationTest(string stateFilename, ITestOutputHelper testOutputHelper)
        : base(stateFilename, testOutputHelper) { }

    protected override IServiceCollection ConfigureServices(string storagePath)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        return services.AddNtsJudge(configuration);
        //        .AddJsonFileStore(x => x.Path = storagePath)
        //        .AddNConventionalServices(Assembly.GetExecutingAssembly());
    }
}
