using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Contracts.Features.Setup.Clubs;
using NTS.Storage.REST;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration;

public sealed class JudgeDependencyInjectionTests
{
    [Fact]
    public async Task JudgeDriver_ResolvesSetupRootRepositoriesFromRestStorage()
    {
        await using var judge = new JudgeDriver(new Uri("http://127.0.0.1:1"), new Uri("http://127.0.0.1:2"));

        _ = judge.GetRequiredService<IKrudFormService<ClubFormModel>>();
        var repository = judge.GetRequiredService<IRepository<Club>>();

        Assert.IsNotType<KrudInMemoryNodeRepository<Club>>(repository);
        Assert.IsType<ClubApiRepository>(repository);
    }
}
