using MudBlazor;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Horses;

public partial class HorseForm
{
    MudTextField<string?> _feiIdField = default!;
    MudTextField<string?> _nameField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Horse.FeiId), () => _feiIdField);
        RegisterInjector(nameof(Horse.Name), () => _nameField);
    }
}
