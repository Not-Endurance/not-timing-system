using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Startlists;
using NoTiming.Domain.Core.Objects.Startlists;
using NoTiming.Domain.Helpers;

namespace NoTiming.Blazor.Components.Startlist;

public class StartlistTableBehind : NStatefulComponent
{
    protected string GateHeader => "Gate";

    [Inject]
    public IStartUpcoming Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected string FormatAthlete(Starter entry)
    {
        return NameRenderingHelper.Render(entry.AthleteName, entry.AthleteNameEnglish, entry.EnglishNames);
    }

    protected string GetTimerKey(Starter entry)
    {
        return $"{entry.PhaseNumber}:{entry.Number}:{entry.Start}";
    }

    protected void Tick()
    {
        Service.Tick();
    }
}
