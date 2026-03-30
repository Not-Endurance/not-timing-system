using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Officials;

namespace NTS.Judge.Tests.Blazor;

public class OfficialShellBehindTests
{
    [Fact]
    public async Task SearchUsersSafe_WhenTermMatchesEmail_ReturnsUserFromEmailLookup()
    {
        var expected = new User("judge@example.com", "Judge Example", id: 17);
        var component = new TestOfficialShellBehind { Users = new TestUserEmailLookup(expected) };

        var result = (await component.Search("judge@example.com")).OfType<User>().ToList();

        var user = Assert.Single(result);
        Assert.Equal(17, user.Id);
        Assert.Equal("judge@example.com", user.Email);
    }

    [Fact]
    public async Task SearchUsersSafe_WhenTermMatchesPartOfName_ReturnsMatchingUser()
    {
        var expected = new User("judge@example.com", "Judge Example", id: 17);
        var component = new TestOfficialShellBehind { Users = new TestUserEmailLookup(expected) };

        var result = (await component.Search("Example")).OfType<User>().ToList();

        var user = Assert.Single(result);
        Assert.Equal(17, user.Id);
        Assert.Equal("judge@example.com", user.Email);
    }

    sealed class TestOfficialShellBehind : OfficialShellBehind
    {
        public new IUserEmailLookup Users
        {
            set => base.Users = value;
        }

        public Task<IEnumerable<User?>> Search(string term)
        {
            return SearchUsersSafe(term, CancellationToken.None);
        }
    }

    sealed class TestUserEmailLookup : IUserEmailLookup
    {
        readonly User? _user;

        public TestUserEmailLookup(User? user)
        {
            _user = user;
        }

        public Task<User?> ReadByEmail(string email)
        {
            return Task.FromResult(
                _user != null && string.Equals(_user.Email, email, StringComparison.OrdinalIgnoreCase) ? _user : null
            );
        }

        public Task<IEnumerable<User>> Search(string term)
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
    }
}
