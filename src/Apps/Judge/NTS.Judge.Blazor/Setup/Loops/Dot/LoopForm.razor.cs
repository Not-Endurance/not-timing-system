using MudBlazor;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.Loops.Dot;

public partial class LoopForm
{
    MudNumericField<double?> _distanceField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Loop.Distance), () => _distanceField);
    }
}
