using Microsoft.AspNetCore.Components;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Domain;
using Not.Krud.Abstractions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Not.Blazor.CRUD.Lists;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public class CrudeListBehind<T, TModel, TForm> : NStatefulComponent
    where T : Entity
    where TModel : IFormModel<T>, new()
    where TForm : NForm<TModel>
{
    [Inject]
    FormManager<TModel, TForm> FormNavigator { get; set; } = default!;

    [Inject] // TODO: Probably refactor this as ICrudParent<T> and make it nullable!!!!
    IEnumerable<IKrudNodeSetter> ParentContexts { get; set; } = default!;

    [Inject]
    protected IListBehind<T> Behind { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter, EditorRequired]
    public string UpdateRoute { get; set; } = default!;

    protected async Task CreateHandler()
    {
        try
        {
            await FormNavigator.Create();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task UpdateHandler(T aggregate)
    {
        try
        {
            SetKrudNode(aggregate);
            var model = CreateModel(aggregate);
            await FormNavigator.Update(UpdateRoute, model);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task DeleteHandler(T aggregate)
    {
        try
        {
            await Behind.Delete(aggregate);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void SetKrudNode(T aggregate)
    {
        foreach (var context in ParentContexts)
        {
            context.SetParent(aggregate);
        }
    }

    TModel CreateModel(T entity)
    {
        var model = new TModel();
        model.FromEntity(entity);
        return model;
    }
}
