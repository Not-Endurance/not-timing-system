using Not.Application.Krud.Abstractions;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Domain.Aggregates;

namespace Not.Blazor.CRUD.Lists;

public partial class CrudList<T, TModel, TForm> : NComponent
    where T : AggregateRoot
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
    public int? ParentId { get; set; } // TODO: can probably be deleted

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter, EditorRequired]
    public string UpdateRoute { get; set; } = default!;

    protected override void OnInitialized()
    {
        GuardHelper.ThrowIfDefault(UpdateRoute);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IEnumerable<object> args = ParentId != null ? [ParentId] : [];
            await Observe(Behind, args);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

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
            await SetKrudNode(aggregate);
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

    async Task SetKrudNode(T aggregate)
    {
        foreach (var context in ParentContexts)
        {
            await context.Set(aggregate);
        }
    }

    TModel CreateModel(T entity)
    {
        var model = new TModel();
        model.FromEntity(entity);
        return model;
    }
}
