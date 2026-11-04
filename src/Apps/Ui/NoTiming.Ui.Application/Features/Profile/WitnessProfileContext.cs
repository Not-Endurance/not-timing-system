using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Aggregates;
using NTS.Witness.Contracts.Features.Profile;

namespace NTS.Witness.Features.Profile;

public class WitnessProfileContext : NStatefulService, IWitnessProfileContext, IScoped
{
    readonly IRepository<Country> _countries;
    readonly IWitnessUserProfileRepository _profiles;
    readonly INUserSession _userSession;
    IReadOnlyList<Country> _countryList = [];

    public WitnessProfileContext(
        INUserSession userSession,
        IRepository<Country> countries,
        IWitnessUserProfileRepository profiles
    )
    {
        _userSession = userSession;
        _countries = countries;
        _profiles = profiles;
    }

    public NUserModel? User { get; private set; }
    public bool RequiresProfileCompletion => User != null && !WitnessProfilePolicy.IsComplete(User);
    public string WelcomeName => WitnessProfilePolicy.ResolveWelcomeName(User);

    protected override async Task<bool> InitializeState()
    {
        var session = await _userSession.GetCurrent<NtsUserSessionStateModel>();
        User = session?.User;
        if (User == null)
        {
            _countryList = [];
            return true;
        }

        _countryList = (await _countries.ReadMany()).OrderBy(country => country.Name).ToArray();
        return true;
    }

    public WitnessProfileFormModel CreateFormModel()
    {
        return WitnessProfileFormModel.From(User, ResolveCountry(User?.CountryRegion));
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

    public async Task<NUserModel?> Save(WitnessProfileFormModel model)
    {
        if (User == null)
        {
            throw new InvalidOperationException("Cannot update profile before the user session is loaded.");
        }

        if (!WitnessProfilePolicy.IsComplete(model))
        {
            throw new InvalidOperationException("Country, first name and last name are required.");
        }

        var result = await _profiles.UpdateProfile(User.Email, model.ToPayload());
        if (!result.IsSuccess || result.Data == null)
        {
            var errors = result.Errors.Length == 0 ? ["Could not update profile."] : result.Errors;
            throw new ApplicationException(string.Join(Environment.NewLine, errors));
        }

        User = result.Data;
        EmitChanged();
        return User;
    }

    Country? ResolveCountry(string? countryRegion)
    {
        if (string.IsNullOrWhiteSpace(countryRegion))
        {
            return null;
        }

        return _countryList.FirstOrDefault(country =>
            string.Equals(country.Name, countryRegion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(country.IsoCode, countryRegion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(country.NfCode, countryRegion, StringComparison.OrdinalIgnoreCase)
        );
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
