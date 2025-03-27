namespace NTS.Relay.ACL.Abstractions;

public static class EmsDomainIdProvider
{
    static readonly Random Random = new();
    static readonly HashSet<int> DomainIds = [];

    public static int Generate()
    {
        var id = Random.Next();
        while (DomainIds.Contains(id))
        {
            id = Random.Next();
        }

        DomainIds.Add(id);
        return id;
    }
}
