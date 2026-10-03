namespace NTS.Tests.Unit;

/// <summary>
/// Readable, stable Guids for fixtures: <c>TestId.Of(7)</c> is always <c>00000007-0000-0000-0000-000000000000</c>.
/// </summary>
public static class TestId
{
    public static Guid Of(int number)
    {
        return new Guid(number, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
