using NTS.Contracts.Setup.Models;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// The families of reference data the Api serves (#603): the Clubs, Horses and Athletes of a Tenant's registry, and the
/// countries of the platform. Each is a name, a collection, who writes it and what it keeps to itself; the routes are
/// the same for all (<see cref="ReferenceEndpoints"/>). An Athlete is linked to an account by a member that holds an email
/// and more, which the resource does not show or take, so a rider cannot be found by the address of the account.
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
        return new("horses", "horses", ReferenceWrites.Registry, new ReferenceMembers<HorseModel>([], []));
    }

    public static ReferenceFamily<AthleteModel, Athlete> Athletes()
    {
        return new(
            "athletes",
            "athletes",
            ReferenceWrites.Registry,
            new ReferenceMembers<AthleteModel>([nameof(AthleteModel.User)], [])
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
