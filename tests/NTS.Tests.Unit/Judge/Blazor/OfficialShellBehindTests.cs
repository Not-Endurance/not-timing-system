using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features.Setup.ConfigureEvents.Officials;
using NTS.Judge.Contracts.Features.Setup;

namespace NTS.Judge.Tests.Blazor;

public class OfficialShellBehindTests
{
    [Fact]
    public async Task SearchUsersSafe_WhenTermMatchesEmail_ReturnsUserFromEmailLookup()
    {
        var expected = new User("judge@example.com", "Judge Example", id: 17);
        var component = new TestOfficialShellBehind { Lookups = new TestJudgeSetupLookupService(expected) };

        var result = (await component.Search("judge@example.com")).OfType<User>().ToList();

        var user = Assert.Single(result);
        Assert.Equal(17, user.Id);
        Assert.Equal("judge@example.com", user.Email);
    }

    [Fact]
    public async Task SearchUsersSafe_WhenTermMatchesPartOfName_ReturnsMatchingUser()
    {
        var expected = new User("judge@example.com", "Judge Example", id: 17);
        var component = new TestOfficialShellBehind { Lookups = new TestJudgeSetupLookupService(expected) };

        var result = (await component.Search("Example")).OfType<User>().ToList();

        var user = Assert.Single(result);
        Assert.Equal(17, user.Id);
        Assert.Equal("judge@example.com", user.Email);
    }

    sealed class TestOfficialShellBehind : OfficialShellBehind
    {
        public new IJudgeSetupLookupService Lookups
        {
            set => base.Lookups = value;
        }

        public Task<IEnumerable<User?>> Search(string term)
        {
            return SearchUsersSafe(term, CancellationToken.None);
        }
    }

    sealed class TestJudgeSetupLookupService : IJudgeSetupLookupService
    {
        readonly User? _user;

        public TestJudgeSetupLookupService(User? user)
        {
            _user = user;
        }

        public Task<IEnumerable<NTS.Domain.Aggregates.Country>> SearchCountries(string term, CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<NTS.Domain.Aggregates.Country>());
        }

        public Task<IEnumerable<Club>> SearchClubs(string term, CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<Club>());
        }

        public Task<IEnumerable<User>> SearchUsers(string term, CancellationToken ct)
        {
            if (_user == null)
            {
                return Task.FromResult<IEnumerable<User>>([]);
            }

            var matches =
                string.IsNullOrWhiteSpace(term)
                || _user.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                || _user.Name.Contains(term, StringComparison.OrdinalIgnoreCase);

            return Task.FromResult<IEnumerable<User>>(matches ? [_user] : []);
        }

        public Task<IEnumerable<Athlete>> SearchAthletes(string term, CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<Athlete>());
        }

        public Task<IEnumerable<Horse>> SearchHorses(string term, CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<Horse>());
        }

        public Task<IEnumerable<NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop>> GetLoops(CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop>());
        }

        public Task<IEnumerable<NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination>> SearchCombinations(
            string term,
            CancellationToken ct
        )
        {
            return Task.FromResult(Enumerable.Empty<NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination>());
        }
    }
}
