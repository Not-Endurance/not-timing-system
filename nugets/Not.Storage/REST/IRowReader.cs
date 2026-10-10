namespace Not.Storage.REST;

/// <summary>
/// A repository that can say whether a row is not there or could not be looked for (#646). A caller that keeps a copy of
/// what is stored removes it when the row is not there, and keeps it when the Api did not answer: a request that failed is
/// not a row that was removed.
/// </summary>
public interface IRowReader<T>
{
    Task<RowRead<T>> Find(Guid id);
}

/// <summary>What a read of one row came to: the row, none when it is not there, and whether the Api could not say.</summary>
public sealed class RowRead<T>
{
    public RowRead(T? row, bool failed)
    {
        Row = row;
        Failed = failed;
    }

    public T? Row { get; }

    /// <summary>The request was not answered, or the Api refused it: <see cref="Row"/> is none, and the row may still be there.</summary>
    public bool Failed { get; }
}
