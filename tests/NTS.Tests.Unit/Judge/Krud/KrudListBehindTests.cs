using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;

namespace NTS.Judge.Tests.Krud;

public class KrudListBehindTests
{
    [Fact]
    public void Actions_AreHiddenByDefault()
    {
        var component = new TestKrudList();

        Assert.Null(component.Create);
        Assert.Null(component.View);
        Assert.Null(component.Update);
        Assert.Null(component.Delete);
    }

    [Fact]
    public void Actions_ShowOnlyWhenEnabled()
    {
        var component = new TestKrudList();
        component.EnableActions();

        Assert.NotNull(component.Create);
        Assert.NotNull(component.View);
        Assert.NotNull(component.Update);
        Assert.NotNull(component.Delete);
    }

    sealed class TestKrudList : KrudListBehind<TestEntity, TestModel, TestShell>
    {
        public Func<Task>? Create => CreateAction;
        public Func<TestEntity, Task>? View => ViewAction;
        public Func<TestEntity, Task>? Update => UpdateAction;
        public Func<TestEntity, Task>? Delete => DeleteAction;

        public void EnableActions()
        {
            AllowCreate = true;
            AllowView = true;
            AllowUpdate = true;
            AllowDelete = true;
        }
    }

    sealed class TestShell : KrudShell<TestModel> { }

    sealed class TestModel : IKrudModel<TestEntity>, IKrudFormModel
    {
        public int? Id { get; set; }

        public void MapFrom(TestEntity entity)
        {
            Id = entity.Id;
        }

        public TestEntity MapToEntity()
        {
            return new TestEntity(Id);
        }
    }

    sealed class TestEntity : Entity
    {
        public TestEntity(int? id = null)
            : base(id) { }

        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
