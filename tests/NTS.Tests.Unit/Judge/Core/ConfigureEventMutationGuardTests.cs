using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Functions;

namespace NTS.Judge.Tests.Core;

public class ConfigureEventMutationGuardTests
{
    [Fact]
    public async Task EnsureCanMutate_WhenEventIsStarted_ThrowsDomainException()
    {
        var guard = CreateGuard(CreateEvent(7, isActive: true, endDay: DateTimeOffset.UtcNow.AddDays(1)));

        var exception = await Assert.ThrowsAsync<DomainException>(() => guard.EnsureCanMutate(7));

        Assert.Contains("7", exception.Message);
        Assert.Contains("started", exception.Message);
    }

    [Fact]
    public async Task EnsureCanMutate_WhenEventIsInactive_AllowsMutation()
    {
        var guard = CreateGuard(CreateEvent(7, isActive: false, endDay: DateTimeOffset.UtcNow.AddDays(1)));

        await guard.EnsureCanMutate(7);
    }

    [Fact]
    public async Task EnsureCanMutate_WhenEventIsStartedButExpired_ThrowsDomainException()
    {
        var guard = CreateGuard(CreateEvent(7, isActive: true, endDay: DateTimeOffset.UtcNow.AddDays(-1)));

        await Assert.ThrowsAsync<DomainException>(() => guard.EnsureCanMutate(7));
    }

    [Fact]
    public async Task EnsureCanMutate_WhenEventIsMissing_AllowsMutation()
    {
        var guard = CreateGuard();

        await guard.EnsureCanMutate(7);
    }

    [Fact]
    public async Task EnsureCanMutate_WhenAnyRequestedEventIsActive_ThrowsDomainException()
    {
        var guard = CreateGuard(
            CreateEvent(7, isActive: false, endDay: DateTimeOffset.UtcNow.AddDays(1)),
            CreateEvent(8, isActive: true, endDay: DateTimeOffset.UtcNow.AddDays(1))
        );

        await Assert.ThrowsAsync<DomainException>(() => guard.EnsureCanMutate([7, 8]));
    }

    static ConfigureEventMutationGuard CreateGuard(params EventInformationModel[] events)
    {
        return new ConfigureEventMutationGuard(new RecordingRepository<EventInformationModel>(events));
    }

    static EventInformationModel CreateEvent(int id, bool isActive, DateTimeOffset endDay)
    {
        return new EventInformationModel
        {
            Id = id,
            Name = $"Event {id}",
            Location = "Sofia",
            StartDay = endDay.AddDays(-1),
            EndDay = endDay,
            IsActive = isActive,
        };
    }

    sealed class RecordingRepository<T> : IRepository<T>
        where T : class
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T> items)
        {
            _items = items.ToList();
        }

        public Task Create(T item)
        {
            throw new NotSupportedException();
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult(_items.FirstOrDefault(filter.Compile()));
        }

        public Task<T?> Read(int id)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            throw new NotSupportedException();
        }

        public Task Update(T item)
        {
            throw new NotSupportedException();
        }

        public Task Delete(T item)
        {
            throw new NotSupportedException();
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            throw new NotSupportedException();
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            throw new NotSupportedException();
        }
    }
}
