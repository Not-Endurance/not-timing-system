using Not.Application.Behinds.Adapters;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Contracts.Features.Settings;

public interface ISettingService : IStatefulService
{
    Setting? Setting { get; }
}
