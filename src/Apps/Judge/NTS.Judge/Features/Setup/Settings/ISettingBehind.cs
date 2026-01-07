using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings;

public interface ISettingBehind : INObservable, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
