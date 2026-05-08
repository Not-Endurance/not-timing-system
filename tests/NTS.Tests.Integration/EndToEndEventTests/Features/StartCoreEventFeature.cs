using Microsoft.Extensions.DependencyInjection;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Contracts.Features.Core;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal sealed class StartCoreEventFeature
{
    readonly JudgeDriver _judge;
    readonly NexusApiDriver _nexusApi;

    public StartCoreEventFeature(JudgeDriver judge, NexusApiDriver nexusApi)
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
