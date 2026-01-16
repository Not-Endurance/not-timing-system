using Not.Application.Behinds.Adapters;
using Not.Application.Services;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Settings;

public interface ISettingBehind : IStatefulService, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
