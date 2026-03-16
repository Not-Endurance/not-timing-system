using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Domain.Krud;
using Not.Krud.Abstractions;
using Not.Krud.ServiceRegistration;

namespace NTS.Judge.Tests.Krud;

public class KrudGraphRegistrationTests
{
    [Fact]
    public async Task RegisterAggregate_ChildRepositoryMutatesAggregateChildren()
    {
        var aggregate = new GraphAggregate(id: 1);
        var aggregateRepository = new AggregateRepository(aggregate);
        var services = new ServiceCollection();
        services.AddSingleton<IRepository<GraphAggregate>>(aggregateRepository);
        services.ConfigureKrud().RegisterAggregate<GraphAggregate>();
        using var provider = services.BuildServiceProvider();

        SetNodeParents(provider, aggregate);
        var repository = provider.GetRequiredService<IRepository<GraphParent>>();
        var parent = new GraphParent("Parent", id: 10);

        await repository.Create(parent);

        Assert.Equal(parent, Assert.Single(aggregate.Parents));
    }

    [Fact]
    public async Task RegisterAggregate_GrandChildRepositoryMutatesSelectedParentChildren()
    {
        var parent = new GraphParent("Parent", id: 10);
        var aggregate = new GraphAggregate([parent], id: 1);
        var aggregateRepository = new AggregateRepository(aggregate);
        var services = new ServiceCollection();
        services.AddSingleton<IRepository<GraphAggregate>>(aggregateRepository);
        services.ConfigureKrud().RegisterAggregate<GraphAggregate>();
        using var provider = services.BuildServiceProvider();

        SetNodeParents(provider, aggregate);
        SetNodeParents(provider, parent);
        var repository = provider.GetRequiredService<IRepository<GraphLeaf>>();
        var leaf = new GraphLeaf("Leaf", id: 20);

        await repository.Create(leaf);

        Assert.Equal(leaf, Assert.Single(parent.Leaves));
    }

    static void SetNodeParents(IServiceProvider provider, object value)
    {
        var setters = provider.GetRequiredService<IEnumerable<IKrudNodeSetter>>();
        foreach (var setter in setters)
        {
            setter.SetParent(value);
        }
    }

    sealed class AggregateRepository : IRepository<GraphAggregate>
    {
        readonly List<GraphAggregate> _items;

        public AggregateRepository(GraphAggregate aggregate)
        {
            _items = [aggregate];
        }

        public Task Create(GraphAggregate item)
        {
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<GraphAggregate?> Read(Expression<Func<GraphAggregate, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(_items.FirstOrDefault(predicate));
        }

        public Task<GraphAggregate?> Read(int id)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<IEnumerable<GraphAggregate>> ReadMany()
        {
            return Task.FromResult<IEnumerable<GraphAggregate>>(_items.ToList());
        }

        public Task<IEnumerable<GraphAggregate>> ReadMany(Expression<Func<GraphAggregate, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<GraphAggregate>>(_items.Where(predicate).ToList());
        }

        public Task Update(GraphAggregate item)
        {
            var index = _items.FindIndex(x => x.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = item;
            }
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task Delete(GraphAggregate item)
        {
            _items.Remove(item);
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<GraphAggregate, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<GraphAggregate> items)
        {
            var set = items.ToHashSet();
            _items.RemoveAll(x => set.Contains(x));
            return Task.CompletedTask;
        }
    }

    sealed class GraphAggregate : Aggregate, IParent<GraphParent>
    {
        readonly List<GraphParent> _parents = [];

        public GraphAggregate(IEnumerable<GraphParent>? parents = null, int? id = null)
            : base(id)
        {
            if (parents != null)
            {
                _parents.AddRange(parents);
            }
        }

        public IReadOnlyList<GraphParent> Parents => _parents.AsReadOnly();

        IReadOnlyList<GraphParent> IParent<GraphParent>.Children => Parents;

        public void Add(GraphParent child)
        {
            _parents.Add(child);
        }

        public void Remove(GraphParent child)
        {
            _parents.Remove(child);
        }

        public void Update(GraphParent child)
        {
            var index = _parents.FindIndex(x => x.Id == child.Id);
            if (index >= 0)
            {
                _parents[index] = child;
            }
        }

        public override string ToString()
        {
            return nameof(GraphAggregate);
        }
    }

    sealed class GraphParent : Entity, IParent<GraphLeaf>
    {
        readonly List<GraphLeaf> _leaves = [];

        public GraphParent(string name, IEnumerable<GraphLeaf>? leaves = null, int? id = null)
            : base(id)
        {
            Name = name;
            if (leaves != null)
            {
                _leaves.AddRange(leaves);
            }
        }

        public string Name { get; }
        public IReadOnlyList<GraphLeaf> Leaves => _leaves.AsReadOnly();

        IReadOnlyList<GraphLeaf> IParent<GraphLeaf>.Children => Leaves;

        public void Add(GraphLeaf child)
        {
            _leaves.Add(child);
        }

        public void Remove(GraphLeaf child)
        {
            _leaves.Remove(child);
        }

        public void Update(GraphLeaf child)
        {
            var index = _leaves.FindIndex(x => x.Id == child.Id);
            if (index >= 0)
            {
                _leaves[index] = child;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    sealed class GraphLeaf : Entity
    {
        public GraphLeaf(string name, int? id = null)
            : base(id)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
