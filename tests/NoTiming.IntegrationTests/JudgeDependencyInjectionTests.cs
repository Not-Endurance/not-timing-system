using NC.Application.CRUD.Ports;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.IntegrationTests.Drivers;
using NoTiming.Judge.Contracts.Features.Setup.Clubs;
using NoTiming.Storage.REST;

namespace NoTiming.IntegrationTests;

public sealed class JudgeDependencyInjectionTests
{
    [Fact]
    public async Task JudgeDriver_ResolvesSetupRootRepositoriesFromRestStorage()
    {
        await using var judge = new JudgeDriver(new Uri("http://127.0.0.1:1"));

        _ = judge.GetRequiredService<IKrudFormService<ClubFormModel>>();
        var repository = judge.GetRequiredService<IRepository<Club>>();

        Assert.IsNotType<KrudInMemoryNodeRepository<Club>>(repository);
        Assert.IsType<ClubApiRepository>(repository);
    }
}
