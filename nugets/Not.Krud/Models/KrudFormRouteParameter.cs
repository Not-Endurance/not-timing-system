using Not.Krud.Abstractions;

namespace Not.Krud.Models;

public sealed class KrudFormRouteParameter<TModel>
    where TModel : IKrudFormModel, new()
{
    public KrudFormRouteParameter(TModel model, bool readOnly)
    {
        Model = model;
        ReadOnly = readOnly;
    }

    public TModel Model { get; }
    public bool ReadOnly { get; }
}
