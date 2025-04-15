using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Results;

public class EmsResult : EmsDomainBase<EmsResultException>, IEmsResultState
{
    [Newtonsoft.Json.JsonConstructor]
    EmsResult() { }

    internal EmsResult(EmsResultType type, string code = null)
        : base(default)
    {
        Code = code;
        Type = type;
    }

    public bool IsNotQualified => Type != EmsResultType.Successful;
    public string Code { get; private set; }
    public EmsResultType Type { get; private set; } = EmsResultType.Successful;
    public string TypeCode
    {
        get
        {
            switch (Type)
            {
                case EmsResultType.Resigned:
                    return "RET";
                case EmsResultType.FailedToQualify:
                    return "FTQ";
                case EmsResultType.Disqualified:
                    return "DSQ";
                case EmsResultType.Successful:
                    return "R";
                case EmsResultType.Invalid:
                default:
                    throw EmsHelper.Create<EmsResultException>("Invalid result type");
            }
        }
    }

    public override string ToString()
    {
        if (Type == EmsResultType.Successful)
        {
            return string.Empty;
        }
        var typeString = string.Empty;

        return $"{typeString.ToUpper()} {TypeCode}";
    }
}

public enum EmsResultType
{
    Invalid = 0,
    Resigned = 1,
    FailedToQualify = 2,
    Disqualified = 3,
    Successful = 4,
}
