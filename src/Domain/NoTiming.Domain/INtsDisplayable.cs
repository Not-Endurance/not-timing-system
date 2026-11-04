using NoTiming.Domain.Enums;

namespace NoTiming.Domain;

public interface INoTimingDisplayable
{
    string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null);
}
