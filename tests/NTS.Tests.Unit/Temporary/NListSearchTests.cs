using Not.Blazor.Components;

namespace NTS.Tests.Unit.Temporary;

public sealed class NListSearchTests
{
    [Fact]
    public void Search_is_disabled_without_search_parameters()
    {
        var list = TestNList.WithoutSearch(new TestItem("Alpha"));

        Assert.False(list.SearchEnabled);
        Assert.Collection(list.Visible, item => Assert.Equal("Alpha", item.Name));
    }

    [Fact]
    public void Search_is_disabled_until_all_search_parameters_are_set()
    {
        var list = TestNList.WithPartialSearch(new TestItem("Alpha"));

        Assert.False(list.SearchEnabled);
    }

    [Fact]
    public void Height_limit_is_enabled_by_default()
    {
        var list = TestNList.WithoutSearch(new TestItem("Alpha"));

        Assert.Contains("n-list-scroll", list.PublicContainerClass);
    }

    [Fact]
    public void Height_limit_can_be_disabled()
    {
        var list = TestNList.WithoutHeightLimit(new TestItem("Alpha"));

        Assert.DoesNotContain("n-list-scroll", list.PublicContainerClass);
    }

    [Fact]
    public async Task Search_with_blank_term_returns_all_items()
    {
        var first = new TestItem("Alpha");
        var second = new TestItem("Beta");
        var list = TestNList.Searchable(first, second);

        await list.Search("");

        Assert.Collection(list.Visible, item => Assert.Same(first, item), item => Assert.Same(second, item));
    }

    [Fact]
    public async Task Search_uses_client_search_function()
    {
        var matching = new TestItem("Alpha Rider", "A-100");
        var other = new TestItem("Alpha Horse", "H-200");
        var list = TestNList.Searchable((item, term) => item.Code == term, matching, other);

        await list.Search("A-100");

        Assert.Collection(list.Visible, item => Assert.Same(matching, item));
    }

    [Fact]
    public async Task Search_passes_trimmed_term_to_client_search_function()
    {
        string? searchedTerm = null;
        var item = new TestItem("Alpha Rider");
        var list = TestNList.Searchable(
            (_, term) =>
            {
                searchedTerm = term;
                return true;
            },
            item
        );

        await list.Search("  Alpha  ");

        Assert.Collection(list.Visible, result => Assert.Same(item, result));
        Assert.Equal("Alpha", searchedTerm);
    }

    [Fact]
    public async Task Search_with_no_matches_returns_empty_resultset()
    {
        var list = TestNList.Searchable(new TestItem("Alpha"), new TestItem("Beta"));

        await list.Search("Gamma");

        Assert.Empty(list.Visible);
    }

    sealed class TestNList : NListBehind<TestItem>
    {
        public static TestNList WithoutSearch(params TestItem[] items)
        {
            return new TestNList { Items = items };
        }

        public static TestNList WithPartialSearch(params TestItem[] items)
        {
            return new TestNList
            {
                Items = items,
                SearchLabel = "Search",
            };
        }

        public static TestNList WithoutHeightLimit(params TestItem[] items)
        {
            return new TestNList
            {
                Items = items,
                NoScroll = true,
            };
        }

        public static TestNList Searchable(params TestItem[] items)
        {
            return new TestNList
            {
                Items = items,
                SearchLabel = "Search",
                SearchItem = (item, term) =>
                    item.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase),
            };
        }

        public static TestNList Searchable(Func<TestItem, string, bool> searchItem, params TestItem[] items)
        {
            return new TestNList
            {
                Items = items,
                SearchLabel = "Search",
                SearchItem = searchItem,
            };
        }

        public bool SearchEnabled => CanSearch;
        public IReadOnlyList<TestItem> Visible => VisibleItems;
        public string PublicContainerClass => ContainerClass;

        public async Task Search(string term)
        {
            await SearchItemsSafe(term, CancellationToken.None);
        }
    }

    sealed class TestItem
    {
        public TestItem(string name, string code = "")
        {
            Name = name;
            Code = code;
        }

        public string Name { get; }
        public string Code { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
