namespace Not.Extensions;
public static class DoubleExtension
{
    public static double FloorWholeNumberToTens(this double number)
    {
        return (double)Math.Floor((decimal)number / 10) * 10;
    }

    public static double FloorWholeNumberToTens(this double? number)
    {
        if(number == null) throw new ArgumentNullException(nameof(number));
        return (double)Math.Floor((decimal)number / 10) * 10;
    }
}
