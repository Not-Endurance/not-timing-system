namespace NTS.Domain.Access;

/// <summary>The policy's answer: the action is allowed, or it is refused and the reason says what is missing.</summary>
public sealed class Verdict
{
    public static Verdict Refused(Refusal reason)
    {
        return new Verdict(reason);
    }

    Verdict(Refusal? reason)
    {
        Reason = reason;
    }

    public static Verdict Allowed { get; } = new(null);

    public bool IsAllowed => Reason == null;
    public Refusal? Reason { get; }
}
