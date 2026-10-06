using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Live;
using NTS.Contracts.Core;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Viewers learn that a Participation changed from the one change notification and nothing else (#623, ADR-0006): the
/// Api's hub, the Ui's own client and store, and the Api they read from, all real.
/// </summary>
public sealed class ViewersFollowChangesTests : IClassFixture<NtsIntegrationFixture>
{
    static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(20);

    readonly NtsIntegrationFixture _fixture;

    public ViewersFollowChangesTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Two_viewers_show_a_Participation_that_appeared_once_the_Api_announces_it_and_not_before()
    {
        var tenant = await TenancySeed.TenantAsync(_fixture.MongoConnectionString);
        var eventId = await EventSeed.LiveAsync(_fixture.MongoConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        await EventSeed.ParticipationAsync(
            _fixture.MongoConnectionString,
            tenant,
            IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid())
        );
        await using var first = new ViewerDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl, null, "viewer-one");
        await using var second = new ViewerDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl, null, "viewer-two");
        await first.Start();
        await second.Start();
        await first.Connect(eventInformation);
        await second.Connect(eventInformation);
        await first.WaitForParticipation(1, _ => true, PATIENCE);
        await second.WaitForParticipation(1, _ => true, PATIENCE);

        var appeared = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        await EventSeed.ParticipationAsync(_fixture.MongoConnectionString, tenant, appeared);
        await Task.Delay(500);

        Assert.Null(first.GetRequiredService<IParticipationStore>().Find(appeared.Id)); // nothing tells them yet
        Assert.Null(second.GetRequiredService<IParticipationStore>().Find(appeared.Id));

        await _fixture.ApiServices.GetRequiredService<IParticipationChanges>().AnnounceAsync(eventId, appeared.Id);

        await first.WaitForParticipation(2, _ => true, PATIENCE);
        await second.WaitForParticipation(2, _ => true, PATIENCE);
        Assert.Equal(appeared.Id, first.GetRequiredService<IParticipationStore>().Find(appeared.Id)?.Id);
        Assert.Equal(appeared.Id, second.GetRequiredService<IParticipationStore>().Find(appeared.Id)?.Id);
    }
}
