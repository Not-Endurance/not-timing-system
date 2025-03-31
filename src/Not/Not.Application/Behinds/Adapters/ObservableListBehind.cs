using Not.Structures;

namespace Not.Application.Behinds.Adapters;

public abstract class ObservableListBehind<T> : ObservableBehind, IDisposable
    where T : IIdentifiable
{
    readonly Guid _loadedSubscriptionId;

    protected ObservableListBehind()
    {
        _loadedSubscriptionId = ObservableList.ChangedEvent.Subscribe(EmitChange);
    }

    protected ObservableListBehind(ObservableList<T> list)
    {
        _loadedSubscriptionId = list.ChangedEvent.Subscribe(EmitChange);
        ObservableList = list;
    }

    protected virtual ObservableList<T> ObservableList { get; set; } = [];

    public void Dispose()
    {
        ObservableList.ChangedEvent.Unsubscribe(_loadedSubscriptionId);
        GC.SuppressFinalize(this);
    }
}
