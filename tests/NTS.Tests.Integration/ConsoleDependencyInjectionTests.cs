using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NoTiming.Ui.Storage.REST;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Contracts.Features.Setup.Clubs;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration;

public sealed class ConsoleDependencyInjectionTests
{
    [Fact]
    public async Task ConsoleDriver_ResolvesSetupRootRepositoriesFromRestStorage()
    {
        await using var console = new ConsoleDriver(new Uri("http://127.0.0.1:1"), new Uri("http://127.0.0.1:2"));

        _ = console.GetRequiredService<IKrudFormService<ClubFormModel>>();
        var repository = console.GetRequiredService<IRepository<Club>>();

        Assert.IsNotType<KrudInMemoryNodeRepository<Club>>(repository);
        Assert.IsType<ClubApiRepository>(repository);
    }
}
