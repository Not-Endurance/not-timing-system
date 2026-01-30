using Newtonsoft.Json;
using Not.Domain.Exceptions;

namespace NTS.Domain.Core.Aggregates.Participations.Objects;

public record Withdrawn : Eliminated
{
    public Withdrawn()
        : base(WITHDRAWN) { }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record Retired : Eliminated
{
    public Retired()
        : base(RETIRED) { }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record Disqualified : Eliminated
{
    public Disqualified()
        : base(DISQUALIFIED) { }

    [JsonConstructor]
    public Disqualified(DisqualifyCode[] dqCodes, string? complement)
        : base(DISQUALIFIED)
    {
        PreventInvalidDisqualify(dqCodes, complement);
        DqCodes = dqCodes;
        Complement = complement; // Doesn't use base ctor with complement, because it is not required here
    }

    public IEnumerable<DisqualifyCode> DqCodes { get; private set; } = [];

    public override string ToString()
    {
        if (DqCodes.Any())
        {
            var codes = string.Join('+', DqCodes.Where(code => code != DisqualifyCode.other));
            return $"DQ {codes}";
        }
        else
        {
            return base.ToString();
        }
    }

    static void PreventInvalidDisqualify(DisqualifyCode[] codes, string? complement)
    {
        if (codes.Length == 0)
        {
            throw new DomainException(Please_provide_reason_to_eliminate_as__, $"{DISQUALIFIED}");
        }
        if (codes.Contains(DisqualifyCode.other) && string.IsNullOrWhiteSpace(complement))
        {
            throw new DomainException(
                Please_provide_reason_to_eliminate_as__,
                $"{DISQUALIFIED} {DisqualifyCode.other}"
            );
        }
    }
}

public record FinishedNotRanked : Eliminated
{
    public FinishedNotRanked(string complement)
        : base(FINISHED_NOT_RANKED, complement) { }

    public override string ToString()
    {
        return base.ToString();
    }
}

public record FailedToQualify : Eliminated
{
    [JsonConstructor]
    public FailedToQualify(FailToQualifyCode[] ftqCodes, string? complement)
        : base(FAILED_TO_QUALIFY)
    {
        PreventInvalidFTC(ftqCodes, complement);
        FtqCodes = ftqCodes;
        Complement = complement; // Doesn't use base ctor with complement, because it is not required here
    }

    public FailedToQualify(FailToQualifyCode[] codes)
        : this(IsNotEmpty(codes), null) { }

    public IEnumerable<FailToQualifyCode> FtqCodes { get; private set; } = [];

    public override string ToString()
    {
        var codes = string.Join('+', FtqCodes);
        return $"FTQ {codes}";
    }

    static FailToQualifyCode[] IsNotEmpty(FailToQualifyCode[] codes)
    {
        if (codes == null || codes.Length == 0)
        {
            throw new DomainException(Select_FTQ_codes);
        }
        if (codes.Contains(FailToQualifyCode.FTC)) { }
        return codes;
    }

    static void PreventInvalidFTC(FailToQualifyCode[] codes, string? complement)
    {
        if (codes.Contains(FailToQualifyCode.FTC) && string.IsNullOrWhiteSpace(complement))
        {
            throw new DomainException(
                Please_provide_reason_to_eliminate_as__,
                $"{FAILED_TO_QUALIFY} {FailToQualifyCode.FTC}"
            );
        }
    }
}

public abstract record Eliminated
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

    protected Eliminated(string eliminationCode, string complement)
        : this(eliminationCode)
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
            throw new DomainException(Please_provide_reason_to_eliminate_as__, eliminationCode);
        }
        return complement;
    }
}
