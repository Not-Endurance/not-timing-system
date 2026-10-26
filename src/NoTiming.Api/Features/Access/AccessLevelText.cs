using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>How a Level is told on the wire: its name in camel case, as every member and every value of the Api is.</summary>
internal static class AccessLevelText
{
    public static string Of(AccessLevel level)
    {
        var name = level.ToString();

        return $"{char.ToLowerInvariant(name[0])}{name[1..]}";
    }
}
