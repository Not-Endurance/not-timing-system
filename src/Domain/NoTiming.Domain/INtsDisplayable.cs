using NTS.Domain.Enums;

namespace NTS.Domain;

public interface INtsDisplayable
{
    string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null);
}
