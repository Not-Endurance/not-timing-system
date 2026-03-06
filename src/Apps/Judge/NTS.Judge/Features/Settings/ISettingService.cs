using Not.Application.Behinds.Adapters;
using Not.Application.Services;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Settings;

public interface ISettingService : IStatefulService, IFormBehind<SettingFormModel>
{
    Setting? Setting { get; }
}
