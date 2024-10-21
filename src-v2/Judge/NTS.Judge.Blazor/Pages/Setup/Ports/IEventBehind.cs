using Not.Blazor.Ports.Behinds;
using NTS.Judge.Blazor.Setup.Events;

namespace NTS.Judge.Blazor.Pages.Setup.Ports;

public interface IEnduranceEventBehind : IFormBehind<EnduranceEventFormModel>, IObservableBehind
{
    EnduranceEventFormModel? Model { get; }
}
