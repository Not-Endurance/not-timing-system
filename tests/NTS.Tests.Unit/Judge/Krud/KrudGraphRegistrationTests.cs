using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Domain.Krud;
using Not.Krud.Abstractions;
using Not.Krud.ServiceRegistration;
using Not.Krud.Services;

namespace NTS.Judge.Tests.Krud;

public class KrudGraphRegistrationTests
{
    [Fact]
    public void ConfigureKrud_DoesNotRegisterOpenGenericRepository()
    {
        var services = new ServiceCollection();

        services.ConfigureKrud();

        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IRepository<>)
                && descriptor.ImplementationType == typeof(KrudInMemoryNodeRepository<>)
        );
    }

    [Fact]
    public void RegisterAggregate_FlatAggregate_DoesNotRegisterRootRepository()
    {
        var services = new ServiceCollection();

        services.ConfigureKrud().RegisterAggregate<FlatAggregate>();

        AssertNoRepositoryRegistration<FlatAggregate>(services);
    }

    [Fact]
    public void RegisterAggregate_GraphAggregate_RegistersOnlyChildRepositories()
    {
        var services = new ServiceCollection();

        services.ConfigureKrud().RegisterAggregate<GraphAggregate>();

        AssertNoRepositoryRegistration<GraphAggregate>(services);
        AssertRepositoryRegistration<GraphParent>(services);
        AssertRepositoryRegistration<GraphLeaf>(services);
    }

    [Fact]
    public void RegisterAggregate_ChildRepositoryAlreadyRegistered_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRepository<GraphParent>>(_ => throw new NotSupportedException());

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.ConfigureKrud().RegisterAggregate<GraphAggregate>()
        );

        Assert.Contains(typeof(GraphParent).FullName!, exception.Message);
    }

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

    static void AssertRepositoryRegistration<T>(IServiceCollection services)
        where T : Entity
    {
        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRepository<T>));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    static void AssertNoRepositoryRegistration<T>(IServiceCollection services)
        where T : Entity
    {
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IRepository<T>));
    }

    sealed class FlatAggregate : Aggregate
    {
        public FlatAggregate(int? id = null)
            : base(id) { }
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

        public Task DeleteMany(Expression<Func<GraphAggregate, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<GraphAggregate> items)
        {
            var set = items.ToHashSet();
            _items.RemoveAll(x => set.Contains(x));
            return Task.CompletedTask;
        }
    }

    sealed class GraphAggregate : Aggregate, IKrudParent<GraphParent>
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

        IReadOnlyList<GraphParent> IKrudParent<GraphParent>.Children => Parents;

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

    sealed class GraphParent : Entity, IKrudParent<GraphLeaf>
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

        IReadOnlyList<GraphLeaf> IKrudParent<GraphLeaf>.Children => Leaves;

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
