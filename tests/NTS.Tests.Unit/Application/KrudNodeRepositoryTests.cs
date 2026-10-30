using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Services;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The lists of the Setup that is open are a Krud list over the children of a node in memory (#635): a list asks the service what is
/// there again after a delete, so the repository has to answer from the node as it is then and not from what it was when the list
/// was made.
/// </summary>
public sealed class KrudNodeRepositoryTests
{
    [Fact]
    public async Task What_a_list_reads_again_after_a_delete_is_what_is_left_of_the_node()
    {
        var node = new Node();
        var kept = new Child("Kept");
        var removed = new Child("Removed");
        node.Add(kept);
        node.Add(removed);
        var repository = new KrudInMemoryNodeRepository<Child>(node, []);
        Assert.Equal([kept, removed], await repository.ReadMany());

        await repository.Delete(removed);

        Assert.Equal([kept], await repository.ReadMany());
    }

    [Fact]
    public async Task What_a_list_reads_again_after_a_cascading_delete_is_what_is_left_of_the_node()
    {
        var node = new Node();
        var kept = new Child("Kept");
        var removed = new Child("Removed");
        node.Add(kept);
        node.Add(removed);
        var repository = new KrudInMemoryNodeRepository<Child>(node, []);

        await repository.DeleteCascade(removed);

        Assert.Equal([kept], await repository.ReadMany());
    }

    sealed class Child : Entity
    {
        readonly string _name;

        public Child(string name)
            : base(null)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }
    }

    sealed class Node : IKrudParentNodeOf<Child>
    {
        readonly List<Child> _children = [];

        public IReadOnlyList<Child> Children => _children;

        public void Add(Child child)
        {
            _children.Add(child);
        }

        public void Remove(Child child)
        {
            _children.Remove(child);
        }

        public void Update(Child child) { }

        public void SetParent(object aggregate) { }
    }
}
