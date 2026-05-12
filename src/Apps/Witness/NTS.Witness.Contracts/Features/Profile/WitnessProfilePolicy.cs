using Not.Application.Authentication.User;

namespace NTS.Witness.Contracts.Features.Profile;

public static class WitnessProfilePolicy
{
    public static bool IsComplete(NUserModel? user)
    {
        return user != null
            && !string.IsNullOrWhiteSpace(user.GivenName)
            && !string.IsNullOrWhiteSpace(user.Surname)
            && !string.IsNullOrWhiteSpace(user.CountryRegion);
    }

    public static bool IsComplete(WitnessProfileFormModel? model)
    {
        return model != null
            && !string.IsNullOrWhiteSpace(model.GivenName)
            && !string.IsNullOrWhiteSpace(model.Surname)
            && model.Country != null;
    }

    public static string ResolveWelcomeName(NUserModel? user)
    {
        return FirstNonEmpty(user?.GivenName, user?.DisplayName, user?.Name, user?.Email) ?? string.Empty;
    }

    static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
