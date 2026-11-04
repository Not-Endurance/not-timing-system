using Microsoft.Extensions.DependencyInjection;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Contracts.Features.Core;
using NoTiming.IntegrationTests.Drivers;

namespace NoTiming.IntegrationTests.EndToEndEventTests.Features;

internal sealed class StartCoreEventFeature
{
    readonly JudgeDriver _judge;
    readonly NoTimingApiDriver _nexusApi;

    public StartCoreEventFeature(JudgeDriver judge, NoTimingApiDriver nexusApi)
    {
        _judge = judge;
        _nexusApi = nexusApi;
    }

    public async Task<EventInformation> Execute(SetupFeatureResult setup)
    {
        var setupEvent = await _nexusApi.ReadSetupConfigureEvent(setup.SetupEvent.Id);

        await _judge.Start();
        await _judge.GetRequiredService<IDashService>().Start(setupEvent.Id);

        return await _nexusApi.ReadEventInformation(setupEvent.Id);
    }
}
