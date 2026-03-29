using Not.Application.Behinds.Adapters;

namespace NTS.Witness.Features.Access;

public interface IWitnessAccessContext : IStatefulService
{
    WitnessAccessLevel AccessLevel { get; }
}
