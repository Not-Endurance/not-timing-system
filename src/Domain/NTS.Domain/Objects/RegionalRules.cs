using NTS.Domain.Enums;

namespace NTS.Domain.Objects;

/// <summary>
/// The rules of the Regional competitions of a Tenant (ADR-0012): whether the speed of a loop is judged on the average
/// only, and which ranker applies. A Tenant Root edits them on the Tenant, and the Event copies them when it starts, as
/// it copies other Setup data, so what an Event was judged by never changes when the Tenant edits them. Phases and
/// Results read them from the Event they belong to; there is no static, which cannot work in one process that serves
/// several Tenants.
/// </summary>
public sealed record RegionalRules
{
    const int MAX_RANKER_CODE_LENGTH = 40;

    public RegionalRules(bool onlyAverageLoopSpeed, string? rankerCode = null)
    {
        OnlyAverageLoopSpeed = onlyAverageLoopSpeed;
        RankerCode = Code(rankerCode);
    }

    /// <summary>The rules of a Tenant that has set none: the FEI's, for every competition.</summary>
    public static RegionalRules None { get; } = new(false);

    /// <summary>Whether a Regional competition judges the speed of a Phase on its loop alone.</summary>
    public bool OnlyAverageLoopSpeed { get; }

    /// <summary>
    /// The code of the regional ranker that ranks the Regional competitions, none for the FEI ranker. A code that no
    /// ranker has ranks like the FEI ranker does.
    /// </summary>
    public string? RankerCode { get; }

    /// <summary>
    /// The code of the regional ranker a competition of the ruleset is ranked by, or none when the FEI ranker ranks it.
    /// Only a Regional competition has a choice: an FEI competition is ranked by the FEI's rules in every Tenant.
    /// </summary>
    public string? RankerFor(CompetitionRuleset ruleset)
    {
        return ruleset == CompetitionRuleset.Regional ? RankerCode : null;
    }

    static string? Code(string? rankerCode)
    {
        if (rankerCode == null)
        {
            return null;
        }

        var code = rankerCode.Trim();
        if (code.Length == 0 || code.Length > MAX_RANKER_CODE_LENGTH || code.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"A ranker code is text of one line of at most {MAX_RANKER_CODE_LENGTH} characters.",
                nameof(rankerCode)
            );
        }

        return code;
    }
}
