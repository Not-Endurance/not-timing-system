using Microsoft.AspNetCore.Components;
using Not.Application.Services;
using Not.Async;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Collections;
using Not.Domain;
using Not.Krud.Abstractions;

namespace Not.Krud.Blazor.Components;

public class KrudeListBehind<T, TModel, TForm> : NStatefulComponent
    where T : Entity
    where TModel : IKrudModel<T>, new()
    where TForm : NForm<TModel>
{
    List<T> _entities = [];

    [Inject]
    FormManager<TModel, TForm> FormNavigator { get; set; } = default!;

    [Inject]
    IEnumerable<IKrudNodeSetter> ParentContexts { get; set; } = default!;

    [Inject]
    IListBehind<T> Service { get; set; } = default!;

    protected IReadOnlyList<T> Entities => _entities.AsReadOnly();

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter, EditorRequired]
    public string UpdateRoute { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _entities = await Service.ReadMany().ToList();
        IsLoading = false;
    }

    protected async Task CreateSafe()
    {
        var model = await FormNavigator.Create();
        if (model == null)
        {
            return;
        }
        _entities.Add(MapEntity(model));
    }

    protected async Task UpdateSafe(T entity)
    {
        SetKrudNodeValue(entity);
        var model = CreateModel(entity);
        await FormNavigator.Update(UpdateRoute, model);
        _entities.Update(entity, NCollectionAction.AddOrUpdate);
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
