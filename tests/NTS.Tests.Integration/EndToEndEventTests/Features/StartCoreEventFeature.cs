using Microsoft.Extensions.DependencyInjection;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Contracts.Features.Core;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal sealed class StartCoreEventFeature
{
    readonly ConsoleDriver _console;
    readonly FunctionsApiDriver _functionsApi;

    public StartCoreEventFeature(ConsoleDriver console, FunctionsApiDriver functionsApi)
    {
        _console = console;
        _functionsApi = functionsApi;
    }

    public async Task<EventInformation> Execute(SetupFeatureResult setup)
    {
        var setupEvent = await _functionsApi.ReadSetupConfigureEvent(setup.SetupEvent.Id);

        await _console.Start();
        await _console.GetRequiredService<IDashService>().Start(setupEvent.Id);

        return await _functionsApi.ReadEventInformation(setupEvent.Id);
    }
}
