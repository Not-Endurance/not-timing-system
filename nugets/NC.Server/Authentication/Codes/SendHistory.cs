namespace NC.Server.Authentication.Codes;

/// <summary>What the counters of the current hour bucket and the last send to the address show.</summary>
public sealed class SendHistory
{
    public int ForAddressThisHour { get; init; }
    public int ForIpThisHour { get; init; }
    public int OverallThisHour { get; init; }
    public DateTimeOffset? LastSentForAddressUtc { get; init; }
}
