namespace NoTiming.Api.Features.Profile;

/// <summary>
/// The rule for the text a person types about themselves (a name, a club, an FEI ID): one line of at most so many
/// characters, with no control character. A line break, a tab or a NUL in a name ends up in lists, mails and logs.
/// </summary>
internal static class OneLineText
{
    public static bool IsValid(string text, int maxLength)
    {
        return text.Length <= maxLength && !text.Any(char.IsControl);
    }
}
