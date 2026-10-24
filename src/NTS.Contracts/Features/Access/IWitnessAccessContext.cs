using Not.Application.Behinds.Adapters;

namespace NTS.Contracts.Features.Access;

public interface IWitnessAccessContext : IStatefulService
{
    WitnessAccessLevel AccessLevel { get; }
}
