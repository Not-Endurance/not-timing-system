using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Settings;

public interface ISettingService : IStatefulService
{
    Setting? Setting { get; }
}
