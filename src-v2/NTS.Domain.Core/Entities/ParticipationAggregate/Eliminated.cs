using Newtonsoft.Json;
using System.ComponentModel;

namespace NTS.Domain.Core.Entities.ParticipationAggregate;

public record Withdrawn : Eliminated
{
    public Withdrawn() : base(WITHDRAWN)
    {
    }
}

public record Retired : Eliminated
{
    public Retired() : base(RETIRED)
    {
    }
}

public record Disqualified : Eliminated
{
    public Disqualified(string complement) : base(DISQUALIFIED, complement)
    {
    }
}

public record FinishedNotRanked : Eliminated
{
    public FinishedNotRanked(string complement) : base(FINISHED_NOT_RANKED, complement)
    {
    }
}

public record FailedToQualify : Eliminated
{
    [JsonConstructor]
    public FailedToQualify(FTQCodes[] codes, string? complement) : base(FAILED_TO_QUALIFY)
    {
        PreventInvalidFTC(codes, complement);
        FtqCodes = codes;
        Complement = complement; // Doesn't use base ctor with complement, because it is not required here
    }
    public FailedToQualify(FTQCodes[] codes) : this(IsNotEmpty(codes), null)
    {
    }

    public IEnumerable<FTQCodes> FtqCodes { get; private set; } = [];

    public override string ToString()
    {
        var codes = string.Join('+', FtqCodes);
        return $"FTQ {codes}";
    }

    static FTQCodes[] IsNotEmpty(FTQCodes[] codes)
    {
        if (codes == null || codes.Length == 0)
        {
            throw new DomainException($"Cannot eliminate as FTQ without FTQ codes");
        }
        if (codes.Contains(FTQCodes.FTC))
        {
        }
        return codes;
    }

    static void PreventInvalidFTC(FTQCodes[] codes, string? complement)
    {
        if (codes.Contains(FTQCodes.FTC) && string.IsNullOrWhiteSpace(complement))
        {
            throw new DomainException($"FEI rules require a written explanation for FTC" +
                $" (Failed to Complete) elimination. Please provide '{nameof(Complement)}'");
        }
    }
}

public enum FTQCodes
{
    /// <summary>
    /// Not respecting applicable speed restrictions
    /// </summary>
    [Description("Not respecting applicable speed restrictions")]
    SP = 1,
    /// <summary>
    /// Irregular Gait
    /// </summary>
    [Description("Irregular Gait")]
    GA = 2,
    /// <summary>
    /// Metabolic issue
    /// </summary>
    [Description("Metabolic issue")]
    ME = 3,
    /// <summary>
    /// Minor injury
    /// </summary>
    [Description("Minor injury")]
    MI = 4,
    /// <summary>
    /// Serious injury (musculoskeletal)
    /// </summary>
    [Description("Serious injury (musculoskeletal)")]
    SIMUSCU = 5,
    /// <summary>
    /// Serious injury (metabolic)
    /// </summary>
    [Description("Serious injury (metabolic)")]
    SIMETA = 6,
    /// <summary>
    /// Catastrophic injury
    /// </summary>
    [Description("Catastrophic injury")]
    CI = 7,
    /// <summary>
    /// Out of time
    /// </summary>
    [Description("Out of time")]
    OT = 8,
    /// <summary>
    /// Failed to complete a Loop, but passes Horse inspection after that Loop. <seealso cref="FailedToQualify.Complement"/> is required
    /// </summary>
    [Description("Failed to complete a Loop, but passes Horse inspection after that Loop.")]
    FTC = 9
}

public abstract record Eliminated : DomainObject
{
    public const string WITHDRAWN = "WD";
    public const string RETIRED = "RET";
    public const string FINISHED_NOT_RANKED = "FNR";
    public const string DISQUALIFIED = "DQ";
    public const string FAILED_TO_QUALIFY = "FTQ";

    protected Eliminated(string eliminationCode)
    {
        Code = eliminationCode;
    }
    protected Eliminated(string eliminationCode, string complement) : this(eliminationCode)
    {
        Complement = IsNotNullOrEmpty(complement, eliminationCode);
    }

    public string Code { get; }
    public string? Complement { get; protected set; }

    public override string ToString()
    {
        return Code;
    }

    static string IsNotNullOrEmpty(string complement, string eliminationCode)
    {
        if (string.IsNullOrWhiteSpace(complement))
        {
            throw new DomainException($"Please provide reason to eliminate as '{eliminationCode}'");
        }
        return complement;
    }
}