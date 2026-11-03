namespace NTS.Domain.Access;

/// <summary>How far a caller reaches at one place, from the lowest. Each Level holds everything the ones below it do (ADR-0012).</summary>
public enum AccessLevel
{
    Anonymous = 0,
    Account = 1,
    Staff = 2,
    ConsoleOperator = 3,
    Root = 4,
}
