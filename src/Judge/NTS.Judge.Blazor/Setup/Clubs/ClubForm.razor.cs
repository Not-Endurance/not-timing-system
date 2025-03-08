using MudBlazor;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Blazor.Setup.Clubs;

public partial class ClubForm
{
    MudTextField<string?> _nameField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Club.Name), () => _nameField);
    }
}
