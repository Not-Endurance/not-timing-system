using NC.Application.Authentication.User;
using NoTiming.Application.Contracts.Account;

namespace NoTiming.Ui.Contracts.Features.Profile;

public static class UiProfilePolicy
{
    public static bool IsComplete(NUserModel? user, NoTimingProfile? profile)
    {
        return user != null
            && !string.IsNullOrWhiteSpace(user.GivenName)
            && !string.IsNullOrWhiteSpace(user.Surname)
            && !string.IsNullOrWhiteSpace(profile?.CountryIsoCode);
    }

    public static bool IsComplete(UiProfileFormModel? model)
    {
        return model != null
            && !string.IsNullOrWhiteSpace(model.GivenName)
            && !string.IsNullOrWhiteSpace(model.Surname)
            && model.Country != null;
    }

    public static string ResolveWelcomeName(NUserModel? user)
    {
        var value = FirstNonEmpty(user?.GivenName, user?.DisplayName, user?.Name, user?.Email);
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
