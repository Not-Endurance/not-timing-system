using System.Linq.Expressions;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// A repository of Participations that a test can hold and release: while <see cref="Hold"/> is on, a read by id (or of
/// all) does not finish until the test releases it, and answers with what the repository holds at that moment. It
/// counts the reads, so a test can say how many were in flight and how many were made in all.
/// </summary>
internal sealed class ControlledParticipationRepository : IEventScopedRepository<Participation>
{
    readonly List<Participation> _items;
    readonly List<(Guid? Id, TaskCompletionSource Release)> _held = [];
    readonly List<Guid> _reads = [];

    public ControlledParticipationRepository(params Participation[] items)
    {
        _items = [.. items];
    }

    /// <summary>When on, every read waits for <see cref="Release"/>.</summary>
    public bool Hold { get; set; }

    /// <summary>When on, a read by id fails, as a connection that dropped would.</summary>
    public bool Fail { get; set; }

    /// <summary>Every read by id, in the order they were made.</summary>
    public List<Guid> Reads
    {
        get
        {
            lock (_reads)
            {
                return [.. _reads];
            }
        }
    }

    public int ReadManyCalls { get; private set; }

    /// <summary>The reads that were made and have not finished.</summary>
    public int InFlight(Guid id)
    {
        return _held.Count(x => x.Id == id);
    }

    /// <summary>Changes what the repository holds, as another writer would.</summary>
    public void Store(Participation participation)
    {
        var index = _items.FindIndex(x => x.Id == participation.Id);
        if (index < 0)
        {
            _items.Add(participation);
        }
        else
        {
            _items[index] = participation;
        }
    }

    public void Remove(Guid id)
    {
        _items.RemoveAll(x => x.Id == id);
    }

    /// <summary>Finishes the oldest held read of this Participation, with what the repository holds now.</summary>
    public async Task Release(Guid id)
    {
        var held = _held.First(x => x.Id == id);
        _held.Remove(held);
        held.Release.SetResult();
        await Task.Yield();
    }

    /// <summary>Finishes the held read of everything.</summary>
    public async Task ReleaseMany()
    {
        var held = _held.First(x => x.Id == null);
        _held.Remove(held);
        held.Release.SetResult();
        await Task.Yield();
    }

    public async Task<Participation?> Read(Guid id)
    {
        lock (_reads)
        {
            _reads.Add(id);
        }

        if (Fail)
        {
            throw new InvalidOperationException("The read failed.");
        }

        if (Hold)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _held.Add((id, release));
            await release.Task;
        }

        return _items.FirstOrDefault(x => x.Id == id);
    }

    public async Task<IEnumerable<Participation>> ReadMany()
    {
        ReadManyCalls++;
        var asItWasWhenAsked = _items.ToArray(); // a read of everything answers as the repository was when it was asked
        if (Hold)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _held.Add((null, release));
            await release.Task;
        }

        return asItWasWhenAsked;
    }

    public Task<Participation?> Read(Expression<Func<Participation, bool>> filter)
    {
        return Task.FromResult(_items.AsQueryable().FirstOrDefault(filter));
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        return Task.FromResult<IEnumerable<Participation>>(_items.AsQueryable().Where(filter).ToArray());
    }

    public Task Create(Participation item)
    {
        Store(item);
        return Task.CompletedTask;
    }

    public Task Update(Participation item)
    {
        Store(item);
        return Task.CompletedTask;
    }

    public Task Delete(Participation item)
    {
        Remove(item.Id);
        return Task.CompletedTask;
    }

    public Task DeleteMany(IEnumerable<Participation> items)
    {
        foreach (var item in items)
        {
            Remove(item.Id);
        }

        return Task.CompletedTask;
    }

    public Task DeleteMany(Expression<Func<Participation, bool>> filter)
    {
        var predicate = filter.Compile();
        _items.RemoveAll(x => predicate(x));
        return Task.CompletedTask;
    }
}
