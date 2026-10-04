using NTS.Domain.Objects;

namespace NTS.Contracts.Shared.Models;

/// <summary>The rules of the Regional competitions as a document: what a Tenant sets and an Event copies (ADR-0012).</summary>
public class RegionalRulesModel
{
    public static RegionalRulesModel From(RegionalRules rules)
    {
        return new RegionalRulesModel
        {
            OnlyAverageLoopSpeed = rules.OnlyAverageLoopSpeed,
            RankerCode = rules.RankerCode,
        };
    }

    public bool OnlyAverageLoopSpeed { get; set; }
    public string? RankerCode { get; set; }

    public RegionalRules MapToEntity()
    {
        return new RegionalRules(OnlyAverageLoopSpeed, RankerCode);
    }
}
