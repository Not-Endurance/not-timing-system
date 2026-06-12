using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Startlists;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Helpers;

namespace NTS.Blazor.Components.Startlist;

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
        return NameRenderingHelper.Render(entry.AthleteName, entry.AthleteNameEnglish, entry.Ruleset);
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
