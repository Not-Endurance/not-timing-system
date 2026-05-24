using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Setup;

public static class UserSearchPolicy
{
    public static bool IsMatch(User user, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return Contains(user.Email, term)
            || Contains(user.Name, term)
            || Contains(user.GivenName, term)
            || Contains(user.Surname, term)
            || Contains(user.DisplayName, term);
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.InvariantCultureIgnoreCase) == true;
    }
}
