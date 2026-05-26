using Microsoft.Extensions.Localization;
using Not.Blazor.Dialogs;
using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Localization;

namespace NTS.Tests.Unit.Temporary;

public sealed class KrudListReadOnlyTests
{
    const string DeleteConfirmationFormat = "Are you sure you want to delete '{0}'?";

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

    [Fact]
    public void Delete_dialog_message_uses_generic_delete_confirmation()
    {
        var localizer = new TestLocalizer();
        LocalizationHelper.Configure(localizer);
        try
        {
            var dialog = TestDeleteDialog.For("Alpha");

            Assert.Equal("Are you sure you want to delete 'Alpha'?", dialog.PublicMessage);
        }
        finally
        {
            LocalizationHelper.Clear(localizer);
        }
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

        public static TestKrudList WithAllowFlags(bool allowCreate, bool allowView, bool allowUpdate, bool allowDelete)
        {
            return new TestKrudList
            {
                AllowCreate = allowCreate,
                AllowView = allowView,
                AllowUpdate = allowUpdate,
                AllowDelete = allowDelete,
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
        readonly string? _name;

        public TestEntity(int? id = null, string? name = null)
            : base(id)
        {
            _name = name;
        }

        public TestEntity(string name)
            : this(id: null, name: name) { }

        public override string ToString()
        {
            return _name ?? nameof(TestEntity);
        }
    }

    sealed class TestDeleteDialog : NDeleteDialogBehind
    {
        public static TestDeleteDialog For(string item)
        {
            return new TestDeleteDialog { Item = item };
        }

        public string PublicMessage => Message;
    }

    sealed class TestLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            new(
                name,
                name == nameof(NStrings.Are_you_sure_you_want_to_delete_0_string) ? DeleteConfirmationFormat : name
            );
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(this[name].Value, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
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
