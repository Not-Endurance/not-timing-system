using Not.Structures;

namespace Not.Application.Adapters.Behinds;

public abstract class ObservableListBehind<T> : ObservableBehind
    where T : IIdentifiable
{
    protected ObservableListBehind()
    {
    }

    protected ObservableListBehind(ObservableList<T> list)
    {
        ObservableList = list;
    }

    protected ObservableList<T> ObservableList { get; } = [];
}
