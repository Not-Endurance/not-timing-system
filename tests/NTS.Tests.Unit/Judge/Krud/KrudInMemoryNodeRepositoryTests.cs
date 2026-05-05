using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using Not.Krud.Services;

namespace NTS.Judge.Tests.Krud;

public class KrudInMemoryNodeRepositoryTests
{
    [Fact]
    public async Task Create_AddsChildThroughParentNode()
    {
        var parent = new TestParentNode();
        var repository = CreateRepository(parent);
        var child = new TestChild("One", 1);

        await repository.Create(child);

        Assert.Equal(1, parent.AddCalls);
        Assert.Equal(child, Assert.Single(parent.InternalChildren));
    }

    [Fact]
    public async Task Read_Filter_ReturnsMatchingChild()
    {
        var child = new TestChild("Match", 1);
        var parent = new TestParentNode([child]);
        var repository = CreateRepository(parent);

        var result = await repository.Read(x => x.Name == "Match");

        Assert.Equal(child, result);
    }

    [Fact]
    public async Task ReadMany_ReturnsAllChildren()
    {
        var first = new TestChild("One", 1);
        var second = new TestChild("Two", 2);
        var parent = new TestParentNode([first, second]);
        var repository = CreateRepository(parent);

        var result = (await repository.ReadMany()).ToList();

        Assert.Equal(new[] { first, second }, result);
    }

    [Fact]
    public async Task ReadMany_Filter_ReturnsFilteredChildren()
    {
        var first = new TestChild("One", 1);
        var second = new TestChild("Two", 2);
        var parent = new TestParentNode([first, second]);
        var repository = CreateRepository(parent);

        var result = (await repository.ReadMany(x => x.Name == "Two")).ToList();

        Assert.Equal([second], result);
    }

    [Fact]
    public async Task Update_CallsParentNodeUpdate()
    {
        var existing = new TestChild("One", 1);
        var updated = new TestChild("Updated", 1);
        var parent = new TestParentNode([existing]);
        var repository = CreateRepository(parent);

        await repository.Update(updated);

        Assert.Equal(1, parent.UpdateCalls);
        Assert.Equal("Updated", Assert.Single(parent.InternalChildren).Name);
    }

    [Fact]
    public async Task Delete_Entity_CallsParentNodeRemove()
    {
        var child = new TestChild("Delete", 1);
        var parent = new TestParentNode([child]);
        var repository = CreateRepository(parent);

        await repository.Delete(child);

        Assert.Equal(1, parent.RemoveCalls);
        Assert.Empty(parent.InternalChildren);
    }

    [Fact]
    public async Task Delete_Filter_RemovesAllMatchingChildren()
    {
        var first = new TestChild("Match", 1);
        var second = new TestChild("Keep", 2);
        var third = new TestChild("Match", 3);
        var parent = new TestParentNode([first, second, third]);
        var repository = CreateRepository(parent);

        await repository.DeleteMany(x => x.Name == "Match");

        Assert.Equal(2, parent.RemoveCalls);
        Assert.Equal([second], parent.InternalChildren);
    }

    [Fact]
    public async Task Delete_Items_RemovesSpecifiedChildren()
    {
        var first = new TestChild("One", 1);
        var second = new TestChild("Two", 2);
        var third = new TestChild("Three", 3);
        var parent = new TestParentNode([first, second, third]);
        var repository = CreateRepository(parent);

        await repository.DeleteMany([first, third]);

        Assert.Equal(2, parent.RemoveCalls);
        Assert.Equal([second], parent.InternalChildren);
    }

    [Fact]
    public async Task PreviewDelete_WithoutResolver_ReturnsNoUsages()
    {
        var entity = new TestChild("Standalone", 1);
        var parent = new TestParentNode([entity]);
        var repository = CreateRepository(parent);

        var impact = await repository.PreviewDelete(entity);

        Assert.False(impact.HasUsages);
        Assert.Equal("Standalone", impact.Target);
    }

    [Fact]
    public async Task PreviewDelete_WithResolver_ReturnsResolverImpact()
    {
        var entity = new TestChild("Standalone", 1);
        var expected = new KrudDeleteImpact(
            "Standalone",
            [new KrudDeleteUsage("TestChild.Reference", "Owner", "Dependent")]
        );
        var resolver = new TestResolver(typeof(TestChild), expected);
        var repository = CreateRepository(new TestParentNode([entity]), [resolver]);

        var impact = await repository.PreviewDelete(entity);

        Assert.Equal(expected, impact);
    }

    [Fact]
    public async Task DeleteCascade_WithResolver_DeletesDependenciesThenTarget()
    {
        var entity = new TestChild("Cascade", 1);
        var resolver = new TestResolver(typeof(TestChild), new KrudDeleteImpact("Cascade", []));
        var parent = new TestParentNode([entity]);
        var repository = CreateRepository(parent, [resolver]);

        await repository.DeleteCascade(entity);

        Assert.Equal(1, resolver.CascadeCalls);
        Assert.Equal(1, parent.RemoveCalls);
        Assert.Empty(parent.InternalChildren);
    }

    [Fact]
    public async Task Read_ById_ThrowsNotImplementedException()
    {
        var repository = CreateRepository(new TestParentNode());

        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Read(1));
    }

    [Fact]
    public async Task Delete_ById_ThrowsNotImplementedException()
    {
        var repository = CreateRepository(new TestParentNode());

        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Delete(1));
    }

    static KrudInMemoryNodeRepository<TestChild> CreateRepository(
        TestParentNode parent,
        IEnumerable<IKrudDependencyResolver>? resolvers = null
    )
    {
        return new KrudInMemoryNodeRepository<TestChild>(parent, resolvers ?? []);
    }

    sealed class TestResolver : IKrudDependencyResolver
    {
        readonly Type _supportedType;
        readonly KrudDeleteImpact _impact;

        public TestResolver(Type supportedType, KrudDeleteImpact impact)
        {
            _supportedType = supportedType;
            _impact = impact;
        }

        public int CascadeCalls { get; private set; }

        public bool Supports(Type principalType)
        {
            return principalType == _supportedType;
        }

        public KrudDeleteImpact PreviewDelete(Entity principal)
        {
            return _impact;
        }

        public void CascadeDeleteDependencies(Entity principal)
        {
            CascadeCalls++;
        }
    }

    sealed class TestParentNode : IKrudParentNodeOf<TestChild>
    {
        readonly List<TestChild> _children;

        public TestParentNode(IEnumerable<TestChild>? children = null)
        {
            _children = children?.ToList() ?? [];
        }

        public int AddCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public List<TestChild> InternalChildren => _children;

        public IReadOnlyList<TestChild> Children => _children.ToList();

        public void SetParent(object aggregate) { }

        public void Add(TestChild child)
        {
            AddCalls++;
            _children.Add(child);
        }

        public void Remove(TestChild child)
        {
            RemoveCalls++;
            _children.Remove(child);
        }

        public void Update(TestChild child)
        {
            UpdateCalls++;
            var index = _children.FindIndex(x => x.Id == child.Id);
            if (index >= 0)
            {
                _children[index] = child;
            }
        }
    }

    sealed class TestChild : Entity
    {
        public TestChild(string name, int? id = null)
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
