
using Not.Random;

namespace Not.DomainUtils;

public static class DomainHelper
{
    public static string Combine(params object?[] values)
    {
        var sections = values.Where(x => x != null);
        return string.Join(" | ", sections);
    }

    public static int GenerateId()
    {
        return RandomHelper.GenerateUniqueInteger();
    }

    public static int EnsureId(int id)
    {
        return id == default ? RandomHelper.GenerateUniqueInteger() : id;
    }
}
