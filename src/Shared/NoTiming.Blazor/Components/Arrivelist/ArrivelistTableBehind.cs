using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NC.Formatting;
using NoTiming.Application.Contracts.Arrivelists;
using NoTiming.Domain.Objects;

namespace NoTiming.Blazor.Components.Arrivelist;

public class ArrivelistTableBehind : NStatefulComponent
{
    const string EMPTY_TIME = "--:--:--";

    [Inject]
    protected IArrivelistService Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected string FormatEstimate(Timestamp? estimate)
    {
        if (estimate == null)
        {
            return EMPTY_TIME;
        }

        var estimateTime = estimate.ToDateTimeOffset();
        var now = DateTimeOffset.Now;
        if (estimateTime <= now)
        {
            return estimate.ToString();
        }

        return FormattingHelper.Format(estimateTime - now);
    }

    protected void Tick()
    {
        Service.Tick();
    }
}
