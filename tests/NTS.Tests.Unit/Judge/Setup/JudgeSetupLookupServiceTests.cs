using System.Linq.Expressions;
using Not.Application.Cache;
using Not.Application.CRUD.Ports;
using Not.Application.Services;
using NTS.Application.Setup;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Judge.Features.Setup;

namespace NTS.Judge.Tests.Setup;

public class JudgeSetupLookupServiceTests
{
    [Fact]
    public async Task SearchUsers_DelegatesToUserLookup()
    {
        var expected = new User("judge@example.com", "Judge Example", id: 17);
        var service = CreateService(users: new TestUserLookup(expected));

        var result = (await service.SearchUsers("Example", CancellationToken.None)).ToList();

        var user = Assert.Single(result);
        Assert.Equal(expected.Id, user.Id);
        Assert.Equal(expected.Email, user.Email);
    }

    [Fact]
    public async Task SearchCombinations_WhenTermMatches_ReturnsMatchingCombination()
    {
        var matching = CreateCombination(41, "John", "Cassini", 1001);
        var nonMatching = CreateCombination(52, "Alice", "Boreas", 1002);
        var service = CreateService(combinations: new RecordingRepository<Combination>([matching, nonMatching]));

        var result = (await service.SearchCombinations("Cass", CancellationToken.None)).ToList();

        Assert.Collection(result, combination => Assert.Equal(matching.Id, combination.Id));
    }

    [Fact]
    public async Task GetLoops_ReturnsAllLoops()
    {
        var loops = new[] { new Loop(40, 1), new Loop(32, 2) };
        var service = CreateService(loops: new RecordingRepository<Loop>(loops));

        var result = (await service.GetLoops(CancellationToken.None)).ToList();

        Assert.Equal([1, 2], result.Select(x => x.Id).ToArray());
    }

    static JudgeSetupLookupService CreateService(
        ICache<Country>? countries = null,
        IRepository<Club>? clubs = null,
        IUserEmailLookup? users = null,
        IRepository<Athlete>? athletes = null,
        IRepository<Horse>? horses = null,
        IRepository<Loop>? loops = null,
        IRepository<Combination>? combinations = null
    )
    {
        return new JudgeSetupLookupService(
            countries ?? new TestCountryCache([]),
            clubs ?? new RecordingRepository<Club>([]),
            users ?? new TestUserLookup(),
            athletes ?? new RecordingRepository<Athlete>([]),
            horses ?? new RecordingRepository<Horse>([]),
            loops ?? new RecordingRepository<Loop>([]),
            combinations ?? new RecordingRepository<Combination>([])
        );
    }

    static Combination CreateCombination(int number, string athleteName, string horseName, int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person([athleteName, "Rider"]), null, country, null, id + 10);
        var horse = new Horse(horseName, null, id + 20);
        return new Combination(number, athlete, horse, id);
    }

    sealed class TestCountryCache : ICache<Country>
    {
        readonly IReadOnlyList<Country> _countries;

        public TestCountryCache(IEnumerable<Country> countries)
        {
            _countries = countries.ToList();
        }

        public void Add(Country entry)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Delete(Country entry)
        {
            throw new NotImplementedException();
        }

        public Task<Country?> Get(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Country>> List()
        {
            return Task.FromResult<IEnumerable<Country>>(_countries);
        }

        public void Update(Country entry)
        {
            throw new NotImplementedException();
        }
    }

    sealed class TestUserLookup : IUserEmailLookup
    {
        readonly IReadOnlyList<User> _users;

        public TestUserLookup(params User[] users)
        {
            _users = users;
        }

        public Task<User?> ReadByEmail(string email)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Email == email));
        }

        public Task<IEnumerable<User>> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Task.FromResult<IEnumerable<User>>(_users);
            }

            return Task.FromResult<IEnumerable<User>>(
                _users.Where(x =>
                    x.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
            );
        }
    }

    sealed class RecordingRepository<T> : IRepository<T>
    {
        readonly IReadOnlyList<T> _items;

        public RecordingRepository(IEnumerable<T> items)
        {
            _items = items.ToList();
        }

        public Task Create(T item)
        {
            throw new NotImplementedException();
        }

        public Task<T?> Read(int id)
        {
            throw new NotImplementedException();
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>(_items);
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<T>>(_items.Where(predicate).ToList());
        }

        public Task Update(T item)
        {
            throw new NotImplementedException();
        }

        public Task Delete(T item)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            throw new NotImplementedException();
        }
    }
}
