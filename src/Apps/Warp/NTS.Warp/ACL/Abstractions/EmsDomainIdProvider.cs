namespace NTS.Warp.ACL.Abstractions;

public static class EmsDomainIdProvider
{
    static readonly Random Random = new();

    public static int Generate()
    {
        var id = Random.Next();
        return id;
    }
}
