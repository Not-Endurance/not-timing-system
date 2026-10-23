using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Formatting;
using NTS.Domain.Objects;

namespace NTS.Blazor.Components.Arrivelist;

/// <summary>
/// One Arrivelist time cell. Renders the FEI estimate, the Regional one, or both stacked and
/// labelled - see <see cref="ArrivelistEstimateMode"/>.
/// </summary>
public class ArrivelistEstimateBehind : NComponent
{
    const string EMPTY_TIME = "--:--:--";

    protected bool ShowFei => Mode != ArrivelistEstimateMode.Regional;
    protected bool ShowRegional => Mode != ArrivelistEstimateMode.Fei;
    protected bool ShowLabels => Mode == ArrivelistEstimateMode.Both;
    protected string FeiEstimate => Format(Fei);
    protected string RegionalEstimate => Format(Regional);

    [Parameter]
    public Timestamp? Fei { get; set; }

    [Parameter]
    public Timestamp? Regional { get; set; }

    [Parameter]
    public ArrivelistEstimateMode Mode { get; set; } = ArrivelistEstimateMode.Fei;

    [Parameter]
    public Color TextColor { get; set; }

    static string Format(Timestamp? estimate)
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
}
