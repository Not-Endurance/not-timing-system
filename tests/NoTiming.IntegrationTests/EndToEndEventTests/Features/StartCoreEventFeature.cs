using Microsoft.Extensions.DependencyInjection;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.IntegrationTests.Drivers;
using NoTiming.Judge.Contracts.Features.Core;

namespace NoTiming.IntegrationTests.EndToEndEventTests.Features;

internal sealed class StartCoreEventFeature
{
    readonly JudgeDriver _judge;
    readonly NoTimingApiDriver _noTimingApi;

    public StartCoreEventFeature(JudgeDriver judge, NoTimingApiDriver noTimingApi)
    {
        _judge = judge;
        _noTimingApi = noTimingApi;
    }

    public async Task<EventInformation> Execute(SetupFeatureResult setup)
    {
        var setupEvent = await _noTimingApi.ReadSetupConfigureEvent(setup.SetupEvent.Id);

        await _judge.Start();
        await _judge.GetRequiredService<IDashService>().Start(setupEvent.Id);

        return await _noTimingApi.ReadEventInformation(setupEvent.Id);
    }
}
