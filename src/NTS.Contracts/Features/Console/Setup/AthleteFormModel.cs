using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// An Athlete of the registry (ADR-0010). The account an Athlete is linked to is the server's alone (the Api neither shows
/// nor takes it), so the form shows none and carries what it read as it was.
/// </summary>
public record AthleteFormModel : KrudFormModel<Athlete>
{
    public string? Name { get; set; }
    public string? NameEnglish { get; set; }
    public string? FeiId { get; set; }
    public Country? Country { get; set; }
    public Club? Club { get; set; }
    public User? User { get; private set; }

    protected override Athlete MapTo()
    {
        return new Athlete(Name, NameEnglish, FeiId, Country, Club, Id, User);
    }

    public override void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Name;
        NameEnglish = athlete.NameEnglish;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club;
        User = athlete.User;
    }
}
