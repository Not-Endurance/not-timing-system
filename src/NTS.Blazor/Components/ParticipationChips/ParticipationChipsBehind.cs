using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.ParticipationChips;

public class ParticipationChipsBehind : NChipsBase<Participation>
{
    protected Action<Participation> _selectAction = default!;

    protected override void OnParametersSet()
    {
        if (SelectAction is not null)
        {
            _selectAction = SelectAction;
        }
    }
}
