using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Console.Features.Registry.Athletes;

public class AthleteShellBehind : KrudShell<AthleteFormModel>
{
    /// <summary>How long a person stops typing before the registries of every Tenant are searched (ADR-0012 limits the search per account).</summary>
    protected const int SEARCH_DEBOUNCE_MILLISECONDS = 500;

    [Inject]
    IConsoleLookups Lookups { get; set; } = default!;

    [Inject]
    IConsoleSameRows SameRows { get; set; } = default!;

    /// <summary>The rows that the Athlete being registered looks like, which the form warns of.</summary>
    protected IReadOnlyList<SameRow> Same { get; private set; } = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // An Athlete that is registered rides for the country of the Tenant unless the person says another.
            if (Model.Id == null && Model.Country == null)
            {
                Model.Country = await Lookups.TenantCountry();
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task Check()
    {
        try
        {
            Same = await SameRows.OfAthlete(Model);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchCountries(term)).Cast<Country?>();
    }

    protected async Task<IEnumerable<Club?>> SearchClubsSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchClubs(term)).Cast<Club?>();
    }
}
