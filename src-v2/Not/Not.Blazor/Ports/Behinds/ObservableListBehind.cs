using Not.Structures;

namespace Not.Blazor.Ports.Behinds;

public abstract class ObservableListBehind<T> : ObservableBehind
    where T : IIdentifiable
{
    protected ObservableListBehind(ObservableList<T> list)
    {
        ObservableList = list;
    }

    protected ObservableList<T> ObservableList { get; } = [];
}
