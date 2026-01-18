using Not.Application.Krud.Abstractions;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Domain.Aggregates;

namespace Not.Blazor.CRUD.Lists;

public partial class CrudList<T, TModel, TForm> : NComponent
    where T : Entity
    where TModel : IFormModel<T>, new()
    where TForm : NForm<TModel>
{
    [Inject]
    IListBehind<T> Behind { get; set; } = default!;

    [Inject]
    FormManager<TModel, TForm> FormNavigator { get; set; } = default!;

    [Inject] // TODO: Probably refactor this as ICrudParent<T> and make it nullable!!!!
    IEnumerable<IKrudNodeSetter> ParentContexts { get; set; } = default!;

    [Parameter]
    public int? ParentId { get; set; } // TODO: DELETE THIS

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
