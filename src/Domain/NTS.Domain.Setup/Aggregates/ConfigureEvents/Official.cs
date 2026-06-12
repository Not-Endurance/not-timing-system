using System.Globalization;
using NTS.Domain.Helpers;
using NTS.Domain.Setup.Aggregates;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Official : Entity, INamed, INtsDisplayable
{
    public Official(string? name, string? nameEnglish, OfficialRole? role, int? id = null, User? user = null)
        : base(id)
    {
        Role = Required(nameof(Role), role);
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        User = user;
    }

    public string Name { get; }
    public string? NameEnglish { get; }
    public OfficialRole Role { get; }
    public User? User { get; }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture);
    }

    public string GetDisplayLabel(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        var values = Role.GetDescription();
        return Combine(values, GetDisplayName(ruleset, culture));
    }

    public override string ToString()
    {
        return GetDisplayLabel();
    }

    public bool IsUniqueRole()
    {
        return Role
            is VeterinaryCommissionPresident
                or GroundJuryPresident
                or ForeignVeterinaryDelegate
                or TechnicalDelegate
                or ForeignJudge;
    }
}
