using Not.Application.Behinds.Adapters;

namespace NoTiming.Ui.Contracts.Features.Access;

public interface IWitnessAccessContext : IStatefulService
{
    WitnessAccessLevel AccessLevel { get; }
}
