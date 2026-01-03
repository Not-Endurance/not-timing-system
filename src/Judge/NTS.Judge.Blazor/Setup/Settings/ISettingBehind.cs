using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Settings;
using NTS.Judge.Blazor.Setup.Settings.Components;

namespace NTS.Judge.Blazor.Setup.Settings;

public interface ISettingBehind : INObservable, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
