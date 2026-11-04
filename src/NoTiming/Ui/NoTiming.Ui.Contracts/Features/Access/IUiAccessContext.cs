using NC.Application.Behinds.Adapters;

namespace NoTiming.Ui.Contracts.Features.Access;

public interface IUiAccessContext : IStatefulService
{
    UiAccessLevel AccessLevel { get; }
}
