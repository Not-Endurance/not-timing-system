using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Formatting;
using NTS.Application.Contracts.Arrivelists;
using NTS.Domain.Objects;

namespace NTS.Blazor.Components.Arrivelist;

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
