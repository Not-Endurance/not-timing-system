using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// An Official of the Setup: the name and the role that the Results print. The account an Official is linked to is the
/// business of the Event's grants (ADR-0012), so the form shows none; a link that a Setup already holds is carried as it was.
/// </summary>
public record OfficialFormModel : KrudFormModel<Official>
{
    public string? Name { get; set; }
    public string? NameEnglish { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;
    public User? User { get; private set; }

    protected override Official MapTo()
    {
        return new Official(Name, NameEnglish, Role, Id, User);
    }

    public override void MapFrom(Official official)
    {
        Id = official.Id;
        Name = official.Name;
        NameEnglish = official.NameEnglish;
        Role = official.Role;
        User = official.User;
    }
}
