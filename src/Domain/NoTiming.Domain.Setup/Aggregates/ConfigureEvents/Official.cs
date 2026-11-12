using System.Globalization;
using NoTiming.Domain.Helpers;
using NoTiming.Domain.Setup.Aggregates;
using static NoTiming.Domain.Enums.OfficialRole;

namespace NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

public class Official : Entity, INamed, INoTimingDisplayable
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

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, englishNames, culture);
    }

    public string GetDisplayLabel(bool englishNames = false, CultureInfo? culture = null)
    {
        var values = Role.GetDescription();
        return Combine(values, GetDisplayName(englishNames, culture));
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
