using NTS.Contracts.Features.Account;

namespace NTS.Contracts.Features.Profile;

public static class WitnessProfilePolicy
{
    public static bool IsComplete(WitnessProfileFormModel? model)
    {
        return model != null
            && !string.IsNullOrWhiteSpace(model.GivenName)
            && !string.IsNullOrWhiteSpace(model.Surname)
            && model.Country != null;
    }

    /// <summary>
    /// What the drawer calls the person: the first name of the profile, else the name of the account, else the address, cut
    /// to what fits. A visitor has no name.
    /// </summary>
    public static string ResolveWelcomeName(AccountProfile? profile, CurrentAccount? account)
    {
        if (account == null)
        {
            return string.Empty;
        }

        var value = FirstNonEmpty(profile?.GivenName, account.Name, account.Email);
        if (value?.Length >= 12)
        {
            value = value[..12];
        }

        return value ?? string.Empty;
    }

    static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
