using Not.Events;
using Not.Observables;

namespace Not.Application.Krud.Nodes;

public class KrudNode : Observer, IObservable
{
    readonly List<KrudNode> _children = [];

    protected Event StateChanged { get; } = new();

    internal bool IsRoot { get; set; }

    public object? Value { get; protected set; }
    public IEventSubscriber Event => StateChanged;

    public IReadOnlyList<KrudNode> Children => _children.AsReadOnly();

    public void Set(object aggregate)
    {
        Value = aggregate;
    }

    internal virtual void AttachChildren(IEnumerable<KrudNode> nodes)
    {
        if (_children.Any())
        {
            return;
        }
        foreach (var node in nodes)
        {
            Observe(node, StateChanged.Emit);
        }
        _children.AddRange(nodes);
    }
}

