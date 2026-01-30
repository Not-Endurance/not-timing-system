namespace Not.Blazor.Components;

public class NListBehind<T> : NStatefulComponent
{
    protected IEnumerable<T> Items { get; private set; } = [];

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public Func<Task>? CreateHandler { get; set; }

    [Parameter, EditorRequired]
    public required Func<Task<IEnumerable<T>>> ReadHandler { get; set; }

    [Parameter]
    public Func<T, Task>? UpdateHandler { get; set; }

    [Parameter]
    public Func<T, Task>? DeleteHandler { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Items = await ReadHandler();
        IsLoading = false;
    }

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
    }
}
