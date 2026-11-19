namespace NC.Application.RPC.Changes;

/// <summary>A change made by some client. The writer lets each client tell its own writes from others'.</summary>
public class ChangeNotice
{
    public string WriterId { get; set; } = string.Empty;

    public bool IsFrom(string clientId)
    {
        return WriterId == clientId;
    }
}
