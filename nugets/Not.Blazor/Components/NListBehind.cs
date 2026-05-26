using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public class NListBehind<T> : NComponent
{
    T? _searchValue;
    string _searchText = string.Empty;

    protected bool CanSearch => SearchLabel != null && SearchItem != null;
    protected string SearchText => _searchText;
    protected T? SearchValue => _searchValue;
    protected IReadOnlyList<T> VisibleItems => SearchItems(_searchText).ToList().AsReadOnly();
    protected string ContainerClass => NoScroll ? "n-content-mid-width" : "n-content-mid-width n-list-scroll";

    [Parameter]
    public IReadOnlyList<T> Items { get; set; } = [];

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public Func<Task>? CreateSafe { get; set; }

    [Parameter]
    public Func<T, Task>? ViewSafe { get; set; }

    [Parameter]
    public Func<T, bool>? CanView { get; set; }

    [Parameter]
    public Func<T, Task>? UpdateSafe { get; set; }

    [Parameter]
    public Func<T, bool>? CanUpdate { get; set; }

    [Parameter]
    public Func<T, Task>? DeleteSafe { get; set; }

    [Parameter]
    public RenderFragment<T>? CustomAction1 { get; set; }

    [Parameter]
    public RenderFragment<T>? CustomAction2 { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool NoScroll { get; set; }

    [Parameter]
    public string? SearchLabel { get; set; }

    [Parameter]
    public Func<T, string, bool>? SearchItem { get; set; }

    protected async Task OnCreate()
    {
        try
        {
            GuardHelper.ThrowIfDefault(CreateSafe);
            await CreateSafe();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnView(T item)
    {
        try
        {
            GuardHelper.ThrowIfDefault(ViewSafe);
            await ViewSafe(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnUpdate(T item)
    {
        try
        {
            GuardHelper.ThrowIfDefault(UpdateSafe);
            await UpdateSafe(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnDelete(T item)
    {
        try
        {
            GuardHelper.ThrowIfDefault(DeleteSafe);
            await DeleteSafe(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Task<IEnumerable<T?>> SearchItemsSafe(string term, CancellationToken _)
    {
        _searchText = term;
        return Task.FromResult(SearchItems(term).Cast<T?>());
    }

    protected Task OnSearchTextChanged(string term)
    {
        _searchText = term;
        return Task.CompletedTask;
    }

    protected Task OnSearchValueChanged(T? item)
    {
        _searchValue = item;
        if (item != null)
        {
            _searchText = ItemToString(item);
        }
        return Task.CompletedTask;
    }

    protected Task ClearSearch()
    {
        _searchText = string.Empty;
        _searchValue = default;
        return Task.CompletedTask;
    }

    protected string ItemToString(T? item)
    {
        return item?.ToString() ?? string.Empty;
    }

    IEnumerable<T> SearchItems(string? term)
    {
        if (!CanSearch || string.IsNullOrWhiteSpace(term))
        {
            return Items;
        }

        var normalizedTerm = term.Trim();
        return Items.Where(x => SearchItem?.Invoke(x, normalizedTerm) ?? false);
    }
}
