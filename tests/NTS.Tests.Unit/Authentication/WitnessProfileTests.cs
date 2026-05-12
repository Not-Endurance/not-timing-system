using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.CRUD.Ports;
using Not.Structures;
using NTS.Domain.Aggregates;
using NTS.Witness.Blazor.Features.Profile;
using NTS.Witness.Contracts.API;
using NTS.Witness.Contracts.Features.Profile;
using NTS.Witness.Features.Profile;

namespace NTS.Authentication.Tests;

public class WitnessProfileTests
{
    [Fact]
    public void Profile_policy_requires_country_first_name_and_last_name_only()
    {
        var emailOnly = new NUserModel("user@example.com");
        var complete = new NUserModel("user@example.com")
        {
            GivenName = "Jane",
            Surname = "Doe",
            CountryRegion = "Bulgaria",
        };

        Assert.False(WitnessProfilePolicy.IsComplete(emailOnly));
        Assert.True(WitnessProfilePolicy.IsComplete(complete));
        Assert.Equal("Jane", WitnessProfilePolicy.ResolveWelcomeName(complete));
    }

    [Fact]
    public void Route_policy_redirects_incomplete_users_except_profile_and_authentication_routes()
    {
        var incomplete = new NUserModel("user@example.com");
        var complete = new NUserModel("user@example.com")
        {
            GivenName = "Jane",
            Surname = "Doe",
            CountryRegion = "Bulgaria",
        };

        Assert.True(WitnessProfileRoutePolicy.ShouldRedirectToProfile(incomplete, "performance"));
        Assert.False(WitnessProfileRoutePolicy.ShouldRedirectToProfile(incomplete, "/profile"));
        Assert.False(WitnessProfileRoutePolicy.ShouldRedirectToProfile(incomplete, "authentication/login"));
        Assert.False(WitnessProfileRoutePolicy.ShouldRedirectToProfile(complete, "performance"));
        Assert.False(WitnessProfileRoutePolicy.ShouldRedirectToProfile(null, "performance"));
    }

    [Fact]
    public async Task Profile_context_searches_countries_and_saves_selected_country_name()
    {
        var bulgaria = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var romania = new Country(2, "Romania", "RO", "ROU", "ro-RO");
        var user = new NUserModel("user@example.com", id: 7);
        var updated = new NUserModel("user@example.com", id: 7)
        {
            GivenName = "Jane",
            MiddleName = "Marie",
            Surname = "Doe",
            CountryRegion = "Bulgaria",
        };
        var profiles = new RecordingProfileRepository { UpdateResult = Result.Success(updated) };
        var context = new WitnessProfileContext(
            new TestUserSession(user),
            new CountryRepository([romania, bulgaria]),
            profiles
        );

        await context.Load();
        var searchResult = await context.SearchCountries("bul", CancellationToken.None);
        var model = context.CreateFormModel();
        model.GivenName = " Jane ";
        model.MiddleName = " Marie ";
        model.Surname = " Doe ";
        model.Country = bulgaria;
        model.Club = " ";

        await context.Save(model);

        var country = Assert.Single(searchResult);
        Assert.Equal("Bulgaria", country.Name);
        Assert.False(context.RequiresProfileCompletion);
        Assert.Equal("Jane", context.WelcomeName);
        Assert.Equal("user@example.com", profiles.LastEmail);
        Assert.NotNull(profiles.LastPayload);
        Assert.Equal("Jane Marie Doe", profiles.LastPayload!.Name);
        Assert.Equal("Bulgaria", profiles.LastPayload.CountryRegion);
        Assert.Null(profiles.LastPayload.Club);
    }

    sealed class TestUserSession : INUserSession
    {
        readonly NUserModel _user;

        public TestUserSession(NUserModel user)
        {
            _user = user;
        }

        public Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
            where TSessionState : class
        {
            INUserSessionModel<TSessionState> session = new NUserSessionModel<TSessionState>("entra-1", _user);
            return Task.FromResult<INUserSessionModel<TSessionState>?>(session);
        }
    }

    sealed class RecordingProfileRepository : IWitnessUserProfileRepository
    {
        public Result<NUserModel>? UpdateResult { get; init; }
        public string? LastEmail { get; private set; }
        public UpdateUserProfilePayload? LastPayload { get; private set; }

        public Task<Result<NUserModel>> UpdateProfile(string email, UpdateUserProfilePayload payload)
        {
            LastEmail = email;
            LastPayload = payload;
            return Task.FromResult(UpdateResult ?? Result.Success(new NUserModel(email)));
        }
    }

    sealed class CountryRepository : IRepository<Country>
    {
        readonly List<Country> _countries;

        public CountryRepository(IEnumerable<Country> countries)
        {
            _countries = countries.ToList();
        }

        public Task Create(Country item)
        {
            _countries.Add(item);
            return Task.CompletedTask;
        }

        public Task<Country?> Read(int id)
        {
            return Task.FromResult(_countries.FirstOrDefault(country => country.Id == id));
        }

        public Task<Country?> Read(System.Linq.Expressions.Expression<Func<Country, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(_countries.FirstOrDefault(predicate));
        }

        public Task<IEnumerable<Country>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Country>>(_countries);
        }

        public Task<IEnumerable<Country>> ReadMany(System.Linq.Expressions.Expression<Func<Country, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Country>>(_countries.Where(predicate).ToList());
        }

        public Task Update(Country item)
        {
            _countries.RemoveAll(country => country.Id == item.Id);
            _countries.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(Country item)
        {
            _countries.RemoveAll(country => country.Id == item.Id);
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<Country> items)
        {
            var ids = items.Select(country => country.Id).ToHashSet();
            _countries.RemoveAll(country => ids.Contains(country.Id));
            return Task.CompletedTask;
        }

        public Task DeleteMany(System.Linq.Expressions.Expression<Func<Country, bool>> filter)
        {
            var predicate = filter.Compile();
            _countries.RemoveAll(country => predicate(country));
            return Task.CompletedTask;
        }
    }
}
