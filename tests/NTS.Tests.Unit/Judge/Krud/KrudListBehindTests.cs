using System.Reflection;
using Not.Domain;
using Not.Blazor.Navigation.Abstractions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Krud.Models;

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

    [Fact]
    public async Task View_WhenNoViewRouteFactory_NavigatesToUpdateRouteWithReadOnlyPayload()
    {
        var navigator = new RecordingNavigator();
        var component = CreateNavigatingList(navigator);
        var entity = new TestEntity(17);

        await component.InvokeView(entity);

        Assert.Equal("/testentity-krud-update-route", navigator.Endpoint);
        var parameter = Assert.IsType<KrudFormRouteParameter<TestModel>>(navigator.Parameter);
        Assert.True(parameter.ReadOnly);
        Assert.Equal(entity.Id, parameter.Model.Id);
    }

    [Fact]
    public async Task Update_NavigatesToUpdateRouteWithEditablePayload()
    {
        var navigator = new RecordingNavigator();
        var component = CreateNavigatingList(navigator);
        var entity = new TestEntity(21);

        await component.InvokeUpdate(entity);

        Assert.Equal("/testentity-krud-update-route", navigator.Endpoint);
        var parameter = Assert.IsType<KrudFormRouteParameter<TestModel>>(navigator.Parameter);
        Assert.False(parameter.ReadOnly);
        Assert.Equal(entity.Id, parameter.Model.Id);
    }

    [Fact]
    public async Task View_WhenViewRouteFactoryIsProvided_UsesCustomViewRoute()
    {
        var navigator = new RecordingNavigator();
        var component = CreateNavigatingList(navigator);
        component.ViewRouteFactory = entity => $"/custom/{entity.Id}";

        await component.InvokeView(new TestEntity(21));

        Assert.Equal("/custom/21", navigator.Endpoint);
        Assert.Null(navigator.Parameter);
    }

    [Fact]
    public async Task ShellUpdate_WhenReadOnly_DoesNotSubmit()
    {
        var submitted = false;
        var shell = new TestShell
        {
            Model = new TestModel { Id = 1 },
            ReadOnly = true,
            OnSubmit = _ =>
            {
                submitted = true;
                return Task.CompletedTask;
            },
        };

        await InvokeShellMethod(shell, "Update");

        Assert.False(submitted);
    }

    static TestKrudList CreateNavigatingList(RecordingNavigator navigator)
    {
        var component = new TestKrudList();
        SetInjected(component, "Navigator", navigator);
        SetInjected(component, "ParentContexts", Enumerable.Empty<IKrudNodeSetter>());
        component.InitializeParameters();
        return component;
    }

    static void SetInjected<TValue>(TestKrudList component, string propertyName, TValue value)
    {
        var property = typeof(KrudListBehind<TestEntity, TestModel, TestShell>).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.NotNull(property);
        property.SetValue(component, value);
    }

    static async Task InvokeShellMethod(TestShell shell, string methodName)
    {
        var method = typeof(KrudShell<TestModel>).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.NotNull(method);
        await (Task)method.Invoke(shell, null)!;
    }

    sealed class TestKrudList : KrudListBehind<TestEntity, TestModel, TestShell>
    {
        public Func<Task>? Create => CreateAction;
        public Func<TestEntity, Task>? View => ViewAction;
        public Func<TestEntity, Task>? Update => UpdateAction;
        public Func<TestEntity, Task>? Delete => DeleteAction;

        public void InitializeParameters()
        {
            OnParametersSet();
        }

        public Task InvokeView(TestEntity entity)
        {
            return ViewSafe(entity);
        }

        public Task InvokeUpdate(TestEntity entity)
        {
            return UpdateSafe(entity);
        }

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

    sealed class RecordingNavigator : ICrumbsNavigator
    {
        public string CurrentEndpoint => Endpoint ?? string.Empty;
        public string? Endpoint { get; private set; }
        public object? Parameter { get; private set; }

        public void NavigateTo(string endpoint)
        {
            Endpoint = endpoint;
            Parameter = null;
        }

        public void NavigateTo<T>(string endpoint, T parameter)
        {
            Endpoint = endpoint;
            Parameter = parameter;
        }

        public bool CanNavigateBack()
        {
            return false;
        }

        public void NavigateBack() { }

        public T ConsumeParameter<T>()
        {
            throw new NotSupportedException();
        }
    }
}
