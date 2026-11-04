using Not.Application.Behinds.Adapters;

namespace NTS.Witness.Contracts.Features.Access;

public interface IWitnessAccessContext : IStatefulService
{
    WitnessAccessLevel AccessLevel { get; }
}
