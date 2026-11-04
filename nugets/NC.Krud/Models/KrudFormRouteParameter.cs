using NC.Krud.Abstractions;

namespace NC.Krud.Models;

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
