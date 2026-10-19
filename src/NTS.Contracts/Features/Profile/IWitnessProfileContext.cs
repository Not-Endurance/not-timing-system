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

    /// <summary>
    /// Bookmarks the Athlete as the person, in the place of the one they had (#639, ADR-0010). The host keeps it and the
    /// profile is read again; an Athlete that is not in a registry is refused.
    /// </summary>
    Task LinkAthlete(Guid athleteId);

    /// <summary>Takes the bookmark of the Athlete away; there is nothing to do when there is none, or it is gone already.</summary>
    Task UnlinkAthlete();

    /// <summary>Adds the Horse to the person's Horses. The host allows at most a hundred.</summary>
    Task AddHorse(Guid horseId);

    /// <summary>Takes the Horse out of the person's Horses; there is nothing to do when it is gone already.</summary>
    Task RemoveHorse(Guid horseId);
}
