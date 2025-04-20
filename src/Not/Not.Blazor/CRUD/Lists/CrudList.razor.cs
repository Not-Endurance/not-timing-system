using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.CRUD.Forms.Components;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Base;

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

    public string EmptyMessage { get; set; } = default!;

    protected override void OnInitialized()
    {
        GuardHelper.ThrowIfDefault(UpdateRoute);
        //Name = Localizer.Get(Name ?? $"{typeof(T).Name}s");
        //TODO: RefactorLocalizer.Get to use string.Format
        EmptyMessage = string.Format(No__have_been_created_for_this_event_string, Name);
    }

    protected override async Task OnInitializedAsync()
    {
        IEnumerable<object> args = ParentId != null ? [ParentId] : [];
        await Observe(Behind, args);
    }

    protected async Task CreateHandler()
    {
        await FormNavigator.Create();
    }

    protected async Task UpdateHandler(T item)
    {
        SetParentContext(item);
        var model = CreateModel(item);
        await FormNavigator.Update(UpdateRoute, model);
    }

    protected async Task DeleteHandler(T item)
    {
        await Behind.Delete(item);
    }

    void SetParentContext(T entity)
    {
        if (entity is IParent parent)
        {
            foreach (var context in ParentContexts)
            {
                context.Set(parent);
            }
        }
    }

    TModel CreateModel(T entity)
    {
        var model = new TModel();
        model.FromEntity(entity);
        return model;
    }
}
