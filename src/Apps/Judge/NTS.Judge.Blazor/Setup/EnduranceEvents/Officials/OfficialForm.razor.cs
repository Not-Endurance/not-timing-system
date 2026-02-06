using MudBlazor;
using Not.Structures;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Officials;

public partial class OfficialForm
{
    MudTextField<string?> _nameField = default!;
    List<NotListModel<OfficialRole>> _roles = NotListModel.FromEnum<OfficialRole>().ToList();

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Official.Person), () => _nameField);
    }
}
