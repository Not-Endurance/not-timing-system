using Microsoft.AspNetCore.Components;
using Not.Async.Extensions;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Collections;
using Not.Domain;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Krud.Models;

namespace Not.Krud.Blazor.Components;

public class KrudListBehind<T, TModel, TShell> : NStatefulComponent
    where T : Entity
    where TModel : IKrudModel<T>, IKrudFormModel, new()
    where TShell : KrudShell<TModel>
{
    Type? _aggregateType;
    List<T> _entities = [];

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    KrudDialogService<TModel, TShell> KrudDialogs { get; set; } = default!;

    [Inject]
    IEnumerable<IKrudNodeSetter> ParentContexts { get; set; } = default!;

    [Inject]
    IKrudListBehind<T> Service { get; set; } = default!;

    protected IReadOnlyList<T> Entities => _entities.AsReadOnly();
    protected Func<Task>? CreateAction => AllowCreate ? CreateSafe : null;
    protected Func<T, Task>? ViewAction => AllowView ? ViewSafe : null;
    protected Func<T, Task>? UpdateAction => AllowUpdate ? UpdateSafe : null;
    protected Func<T, Task>? DeleteAction => AllowDelete ? DeleteSafe : null;

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter]
    public string UpdateRoute { get; set; } = default!;

    [Parameter]
    public Func<T, string>? ViewRouteFactory { get; set; }

    [Parameter]
    public bool AllowCreate { get; set; }

    [Parameter]
    public bool AllowView { get; set; }

    [Parameter]
    public bool AllowUpdate { get; set; }

    [Parameter]
    public bool AllowDelete { get; set; }

    [Parameter]
    public RenderFragment<T>? CustomAction1 { get; set; }

    [Parameter]
    public RenderFragment<T>? CustomAction2 { get; set; }

    [Parameter]
    public Func<T, bool>? CanUpdate { get; set; }

    [Parameter]
    public Func<T, bool>? CanView { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _entities = await Service.ReadMany().ToList();
        IsLoading = false;
    }

    protected override void OnParametersSet()
    {
        if (UpdateRoute == null)
        {
            var aggregate = (_aggregateType ??= typeof(T)).Name.ToLower();
            UpdateRoute = $"/{aggregate}-krud-update-route";
        }
    }

    protected async Task CreateSafe()
    {
        var model = await KrudDialogs.ShowCreateForm();
        if (model == null)
        {
            return;
        }
        _entities.Add(MapEntity(model));
    }

    protected Task UpdateSafe(T entity)
    {
        SetKrudNodeValue(entity);
        var model = CreateModel(entity);
        Navigator.NavigateTo(UpdateRoute, new KrudFormRouteParameter<TModel>(model, false));
        _entities.Update(entity, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    protected Task ViewSafe(T entity)
    {
        if (ViewRouteFactory != null)
        {
            Navigator.NavigateTo(ViewRouteFactory(entity));
            return Task.CompletedTask;
        }

        SetKrudNodeValue(entity);
        var model = CreateModel(entity);
        Navigator.NavigateTo(UpdateRoute, new KrudFormRouteParameter<TModel>(model, true));
        return Task.CompletedTask;
    }

    protected async Task DeleteSafe(T entity)
    {
        var impact = await Service.PreviewDelete(entity);
        if (impact.HasUsages)
        {
            if (!await KrudDialogs.ShowCascadingDeleteConfirmation(impact))
            {
                return;
            }
            await Service.DeleteCascade(entity);
        }
        else
        {
            await Service.Delete(entity);
        }
        _entities.Remove(entity);
    }

    void SetKrudNodeValue(T entity)
    {
        foreach (var context in ParentContexts)
        {
            context.SetParent(entity);
        }
    }

    TModel CreateModel(T entity)
    {
        var model = new TModel();
        model.MapFrom(entity);
        return model;
    }

    T MapEntity(TModel model)
    {
        return model.MapToEntity();
    }
}
