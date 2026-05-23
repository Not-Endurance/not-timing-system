using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;

namespace NTS.Tests.Unit.Temporary;

public sealed class KrudListReadOnlyTests
{
    [Fact]
    public void ReadOnly_true_short_circuits_to_view_only()
    {
        var list = TestKrudList.WithReadOnlyShortcut(
            readOnly: true,
            allowCreate: true,
            allowView: false,
            allowUpdate: true,
            allowDelete: true
        );

        Assert.Null(list.Create);
        Assert.NotNull(list.View);
        Assert.Null(list.Update);
        Assert.Null(list.Delete);
    }

    [Fact]
    public void ReadOnly_false_short_circuits_to_edit_actions()
    {
        var list = TestKrudList.WithReadOnlyShortcut(
            readOnly: false,
            allowCreate: false,
            allowView: true,
            allowUpdate: false,
            allowDelete: false
        );

        Assert.NotNull(list.Create);
        Assert.Null(list.View);
        Assert.NotNull(list.Update);
        Assert.NotNull(list.Delete);
    }

    [Fact]
    public void Missing_ReadOnly_uses_individual_allow_flags()
    {
        var list = TestKrudList.WithAllowFlags(
            allowCreate: false,
            allowView: true,
            allowUpdate: false,
            allowDelete: true
        );

        Assert.Null(list.Create);
        Assert.NotNull(list.View);
        Assert.Null(list.Update);
        Assert.NotNull(list.Delete);
    }

    sealed class TestKrudList : KrudListBehind<TestEntity, TestModel, TestShell>
    {
        public static TestKrudList WithReadOnlyShortcut(
            bool readOnly,
            bool allowCreate,
            bool allowView,
            bool allowUpdate,
            bool allowDelete
        )
        {
            var list = WithAllowFlags(allowCreate, allowView, allowUpdate, allowDelete);
            list.ReadOnly = readOnly;
            return list;
        }

        public static TestKrudList WithAllowFlags(
            bool allowCreate,
            bool allowView,
            bool allowUpdate,
            bool allowDelete
        )
        {
            return new TestKrudList
            {
                AllowCreate = allowCreate,
                AllowView = allowView,
                AllowUpdate = allowUpdate,
                AllowDelete = allowDelete
            };
        }

        public Func<Task>? Create => CreateAction;
        public Func<TestEntity, Task>? View => ViewAction;
        public Func<TestEntity, Task>? Update => UpdateAction;
        public Func<TestEntity, Task>? Delete => DeleteAction;
    }

    sealed class TestShell : KrudShell<TestModel> { }

    sealed class TestEntity : Entity
    {
        public TestEntity(int? id = null)
            : base(id) { }

        public override string ToString()
        {
            return nameof(TestEntity);
        }
    }

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
}
