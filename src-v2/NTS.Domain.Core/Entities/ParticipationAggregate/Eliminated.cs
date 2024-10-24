using Newtonsoft.Json;
using System.ComponentModel;

namespace NTS.Domain.Core.Entities.ParticipationAggregate;

public record Withdrawn : Eliminated
{
    public Withdrawn() : base(WITHDRAWN)
    {
    }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record Retired : Eliminated
{
    public Retired() : base(RETIRED)
    {
    }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record Disqualified : Eliminated
{
    public Disqualified(string complement) : base(DISQUALIFIED, complement)
    {
    }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record FinishedNotRanked : Eliminated
{
    public FinishedNotRanked(string complement) : base(FINISHED_NOT_RANKED, complement)
    {
    }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record FailedToQualify : Eliminated
{
    [JsonConstructor]
    public FailedToQualify(FtqCode[] ftqCodes, string? complement) : base(FAILED_TO_QUALIFY)
    {
        PreventInvalidFTC(ftqCodes, complement);
        FtqCodes = ftqCodes;
        Complement = complement; // Doesn't use base ctor with complement, because it is not required here
    }
    public FailedToQualify(FtqCode[] codes) : this(IsNotEmpty(codes), null)
    {
    }

    public IEnumerable<FtqCode> FtqCodes { get; private set; } = [];

    public override string ToString()
    {
        var codes = string.Join('+', FtqCodes);
        return $"FTQ {codes}";
    }

    static FtqCode[] IsNotEmpty(FtqCode[] codes)
    {
        if (codes == null || codes.Length == 0)
        {
            throw new DomainException($"Cannot eliminate as FTQ without FTQ codes");
        }
        if (codes.Contains(FtqCode.FTC))
        {
        }
        return codes;
    }

    static void PreventInvalidFTC(FtqCode[] codes, string? complement)
    {
        if (codes.Contains(FtqCode.FTC) && string.IsNullOrWhiteSpace(complement))
        {
            throw new DomainException($"FEI rules require a written explanation for FTC" +
                $" (Failed to Complete) elimination. Please provide '{nameof(Complement)}'");
        }
    }
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