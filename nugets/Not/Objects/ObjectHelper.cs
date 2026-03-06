namespace Not.Objects;

public static class ObjectHelper
{
    public static bool AreEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        return a?.GetHashCode() == b?.GetHashCode() && a?.GetType() == b?.GetType();
    }
}
