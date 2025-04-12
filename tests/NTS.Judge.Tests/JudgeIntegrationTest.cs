using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC;
using Not.Filesystem;
using Not.Injection;
using Not.Storage.Stores;
using Not.Tests;
using NTS.Application;
using Xunit.Abstractions;

namespace NTS.Judge.Tests;

public abstract class JudgeIntegrationTest : IntegrationTest
{
    protected JudgeIntegrationTest(string stateFilename, ITestOutputHelper testOutputHelper)
        : base(stateFilename, testOutputHelper) { }

    protected override IServiceCollection ConfigureServices(string storagePath)
    {
        FileContextHelper.SetDebugRootDirectory("nts");
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        return services
            .ConfigureJudge(configuration)
            .AddRpcSocket(RpcProtocol.Http, "localhost",  ApplicationConstants.JUDGE_HUB, ApplicationConstants.RPC_PORT)
            .AddJsonFileStore(x => x.Path = storagePath)
            .RegisterConventionalServices();
    }
}
