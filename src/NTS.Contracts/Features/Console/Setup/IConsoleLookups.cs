using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// What the forms of the Setup and of the registry pick from (#646). The Athletes, Horses and Clubs are the Tenant's own
/// and, for a term of three characters or more, those of every Tenant (ADR-0010, ADR-0012): another Tenant's row is used by
/// its id and is not edited. The Loops and the Combinations are those of the Setup that is open.
/// </summary>
public interface IConsoleLookups
{
    /// <summary>The countries whose name has the term in it.</summary>
    Task<IReadOnlyList<Country>> SearchCountries(string term);

    /// <summary>The country of the Tenant the person acts in, which is the country of the Events it makes; none when it is not known.</summary>
    Task<Country?> TenantCountry();

    Task<IReadOnlyList<Club>> SearchClubs(string term);

    Task<IReadOnlyList<Athlete>> SearchAthletes(string term);

    Task<IReadOnlyList<Horse>> SearchHorses(string term);

    /// <summary>The Loops of the Setup that is open.</summary>
    Task<IReadOnlyList<Loop>> GetLoops();

    /// <summary>The Combinations of the Setup that is open, those the term names or all of them when it names nothing.</summary>
    Task<IReadOnlyList<Combination>> SearchCombinations(string term);
}
