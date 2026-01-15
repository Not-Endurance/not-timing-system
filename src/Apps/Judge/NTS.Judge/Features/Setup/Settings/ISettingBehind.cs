using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Settings;

public interface ISettingBehind : INObservable, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
