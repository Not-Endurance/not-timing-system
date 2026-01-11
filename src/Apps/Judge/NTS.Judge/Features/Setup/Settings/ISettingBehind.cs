using Not.Application.Behinds.Adapters;
using Not.Application.Services;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings;

public interface ISettingBehind : IStatefulService, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
