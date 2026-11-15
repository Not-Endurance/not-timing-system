using NC.Application.Authentication.Abstractions;
using NC.Application.Authentication.User;
using NC.Application.Behinds.Adapters;
using NC.Application.CRUD.Ports;
using NC.Injection;
using NoTiming.Application.Contracts.Account;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Aggregates;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Application.Features.Profile;

public class UiProfileContext : NStatefulService, IUiProfileContext, IScoped
{
    readonly IRepository<Country> _countries;
    readonly IUiUserProfileRepository _profiles;
    readonly INUserSession _userSession;
    IReadOnlyList<Country> _countryList = [];

    public UiProfileContext(
        INUserSession userSession,
        IRepository<Country> countries,
        IUiUserProfileRepository profiles
    )
    {
        _userSession = userSession;
        _countries = countries;
        _profiles = profiles;
    }

    public NUserModel? User { get; private set; }
    public NoTimingProfile? Profile { get; private set; }
    public bool RequiresProfileCompletion => User != null && !UiProfilePolicy.IsComplete(User, Profile);
    public string WelcomeName => UiProfilePolicy.ResolveWelcomeName(User);

    protected override async Task<bool> InitializeState()
    {
        var session = await _userSession.GetCurrent<NoTimingUserSessionStateModel>();
        User = session?.User;
        Profile = null;
        if (User == null)
        {
            _countryList = [];
            return true;
        }

        var signedIn = await _profiles.Read(User.Email);
        Profile = signedIn.Data?.Profile;
        _countryList = (await _countries.ReadMany()).OrderBy(country => country.Name).ToArray();
        return true;
    }

    public UiProfileFormModel CreateFormModel()
    {
        return UiProfileFormModel.From(User, Profile, ResolveCountry(Profile?.CountryIsoCode));
    }

    public Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult<IEnumerable<Country>>(_countryList);
        }

        var normalizedTerm = term.Trim();
        return Task.FromResult<IEnumerable<Country>>(
            _countryList.Where(country =>
                Contains(country.Name, normalizedTerm)
                || Contains(country.IsoCode, normalizedTerm)
                || Contains(country.NfCode, normalizedTerm)
            )
        );
    }

    public async Task<NUserModel?> Save(UiProfileFormModel model)
    {
        if (User == null)
        {
            throw new InvalidOperationException("Cannot update profile before the user session is loaded.");
        }

        if (!UiProfilePolicy.IsComplete(model))
        {
            throw new InvalidOperationException("Country, first name and last name are required.");
        }

        var result = await _profiles.UpdateProfile(User.Email, model.ToPayload());
        if (!result.IsSuccess || result.Data == null)
        {
            var errors = result.Errors.Length == 0 ? ["Could not update profile."] : result.Errors;
            throw new ApplicationException(string.Join(Environment.NewLine, errors));
        }

        User = result.Data.User;
        Profile = result.Data.Profile;
        EmitChanged();
        return User;
    }

    Country? ResolveCountry(string? countryIsoCode)
    {
        if (string.IsNullOrWhiteSpace(countryIsoCode))
        {
            return null;
        }

        return _countryList.FirstOrDefault(country =>
            string.Equals(country.Name, countryIsoCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(country.IsoCode, countryIsoCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(country.NfCode, countryIsoCode, StringComparison.OrdinalIgnoreCase)
        );
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
