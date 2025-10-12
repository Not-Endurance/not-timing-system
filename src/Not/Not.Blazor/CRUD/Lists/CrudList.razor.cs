using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Base;
using Not.Safe;

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
    IEnumerable<ICrudeParentContext> ParentContexts { get; set; } = default!;

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

    protected async Task UpdateHandler(T item)
    {
        try
        {
            await SetParentContext(item);
            var model = CreateModel(item);
            await FormNavigator.Update(UpdateRoute, model);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task DeleteHandler(T item)
    {
        try
        {
            await Behind.Delete(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task SetParentContext(T entity)
    {
        if (entity is not IParent parent)
        {
            return;
        }
        foreach (var context in ParentContexts)
        {
            await context.Set(parent);
        }
    }

    TModel CreateModel(T entity)
    {
        var model = new TModel();
        model.FromEntity(entity);
        return model;
    }
}
