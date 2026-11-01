using NTS.Contracts.Setup.Models;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// The families of reference data the Api serves (#603): the Clubs, Horses and Athletes of a Tenant's registry, and the
/// countries of the platform. Each is a name, a collection, who writes it and what it keeps to itself; the routes are
/// the same for all (<see cref="ReferenceEndpoints"/>). An Athlete is no longer linked to an account (#635, ADR-0010): a
/// document that still holds the old link is read without it, and an edit leaves it as it is.
/// </summary>
internal static class ReferenceFamilies
{
    public static IEndpointRouteBuilder MapReference(this IEndpointRouteBuilder app)
    {
        app.Map(Clubs());
        app.Map(Horses());
        app.Map(Athletes());
        app.Map(Countries());
        return app;
    }

    public static ReferenceFamily<ClubModel, Club> Clubs()
    {
        return new("clubs", "clubs", ReferenceWrites.Registry, new ReferenceMembers<ClubModel>([], []));
    }

    public static ReferenceFamily<HorseModel, Horse> Horses()
    {
        return new(
            "horses",
            "horses",
            ReferenceWrites.Registry,
            new ReferenceMembers<HorseModel>([], []),
            inUse: new ReferenceInUse(
                "Horse",
                "horse-in-use",
                "A Participation still names the Horse.",
                "The copies of a Horse that a Participation keeps carry the id of its row, which is what ties them together: it stays while an Event has them."
            )
        );
    }

    public static ReferenceFamily<AthleteModel, Athlete> Athletes()
    {
        return new(
            "athletes",
            "athletes",
            ReferenceWrites.Registry,
            new ReferenceMembers<AthleteModel>([], []),
            inUse: new ReferenceInUse(
                "Athlete",
                "athlete-in-use",
                "A Participation still names the Athlete.",
                "The copies of an Athlete that a Participation keeps carry the id of their row, which is what ties them together: it stays while an Event has them."
            )
        );
    }

    public static ReferenceFamily<CountryModel, Country> Countries()
    {
        return new(
            "countries",
            "countries",
            ReferenceWrites.Developer,
            new ReferenceMembers<CountryModel>([], []),
            isGlobal: true,
            canDelete: false
        );
    }
}
