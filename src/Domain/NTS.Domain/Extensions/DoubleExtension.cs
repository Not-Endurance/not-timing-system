namespace NTS.Domain.Extensions;

public static class DoubleExtension
{
    public static int RoundNumberToTens(this double number)
    {
        return (int)Math.Floor(number / 10) * 10;
    }
}
