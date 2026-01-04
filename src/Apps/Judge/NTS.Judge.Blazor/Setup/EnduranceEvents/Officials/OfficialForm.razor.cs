using MudBlazor;
using Not.Blazor.Components;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Officials;

public partial class OfficialForm
{
    MudTextField<string?> _nameField = default!;
    NSelect<OfficialRole> _roleField = default!;
    List<NotListModel<OfficialRole>> _roles = NotListModel.FromEnum<OfficialRole>().ToList();

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Official.Person), () => _nameField);
        RegisterInjector(nameof(Official.Role), () => _roleField);
    }
}
