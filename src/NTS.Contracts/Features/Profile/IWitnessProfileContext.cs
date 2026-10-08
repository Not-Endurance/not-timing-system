using Not.Application.Behinds.Adapters;
using NTS.Domain.Aggregates;

namespace NTS.Contracts.Features.Profile;

public interface IWitnessProfileContext : IStatefulService
{
    /// <summary>The profile the host keeps of the person signed in; none for a visitor.</summary>
    AccountProfile? Profile { get; }

    /// <summary>Whether the person is signed in and has not yet named a first name, a surname and a country.</summary>
    bool RequiresProfileCompletion { get; }

    string WelcomeName { get; }
    WitnessProfileFormModel CreateFormModel();
    Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct);

    /// <summary>Saves the profile at the host and returns what the host keeps. A profile the host refuses is not saved.</summary>
    Task<AccountProfile> Save(WitnessProfileFormModel model);
}
