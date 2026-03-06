using Microsoft.AspNetCore.Components;
using Not.Application.Services;
using Not.Async.Extensions;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Collections;
using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;

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
    KrudDialogService<TModel, TShell> DialogService { get; set; } = default!;

    [Inject]
    IEnumerable<IKrudNodeSetter> ParentContexts { get; set; } = default!;

    [Inject]
    IListBehind<T> Service { get; set; } = default!;

    protected IReadOnlyList<T> Entities => _entities.AsReadOnly();

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter]
    public string UpdateRoute { get; set; } = default!;

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
            UpdateRoute = $"/{aggregate}-update";
        }
    }

    protected async Task CreateSafe()
    {
        var model = await DialogService.ShowCreateForm();
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
        Navigator.NavigateTo(UpdateRoute, model);
        _entities.Update(entity, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    protected async Task DeleteSafe(T entity)
    {
        await Service.Delete(entity);
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
