using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using NTS.Domain.Aggregates;

namespace NTS.Witness.Contracts.Features.Profile;

public interface IWitnessProfileContext : IStatefulService
{
    NUserModel? User { get; }
    bool RequiresProfileCompletion { get; }
    string WelcomeName { get; }
    WitnessProfileFormModel CreateFormModel();
    Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct);
    Task<NUserModel?> Save(WitnessProfileFormModel model);
}
