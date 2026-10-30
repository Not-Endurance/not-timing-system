namespace Not.Application.CRUD.Ports;

/// <summary>
/// Writes that are backed up until the remote has answered for good. The backup holds only what the remote has not confirmed, so that
/// a change survives the remote being slow or failing; the remote stays the ultimate storage. What the remote is asked, and what its
/// answer means, is for the caller to say.
/// </summary>
public interface IDurableRepository<T>
{
    /// <summary>
    /// Backs up the item under the id, then asks the remote with it. Returns true when the remote gave a final answer, and then the
    /// backup is removed. When the remote did not answer for good, or failed, the backup stays to be tried again. A backup that cannot
    /// be written does not hold the request back: the remote is asked all the same, and the item is kept only as far as the caller keeps it.
    /// </summary>
    Task<bool> StoreDurably(Guid id, T item, Func<T, Task<bool>> send);

    /// <summary>
    /// The backups that match, of every id: what the remote has not answered for good, for the owner the match names. A backup that
    /// is not an item is removed.
    /// </summary>
    Task<IReadOnlyList<T>> ReadPendingDurably(Func<T, bool> match);

    /// <summary>Keeps the backup of the id as the item is now, without asking the remote: for an item that waits and has changed.</summary>
    Task KeepDurably(Guid id, T item);

    /// <summary>Removes the backup of the id without asking the remote: for an item that no longer needs to be sent.</summary>
    Task ForgetDurably(Guid id);

    /// <summary>
    /// Tries the round again after a wait, for as long as <paramref name="canRetry"/> holds. The wait is five seconds at first, then it
    /// doubles, up to a minute. The round reports true when nothing is left to try. A cancellation of <paramref name="stop"/> ends it.
    /// </summary>
    Task RetryDurably(Func<bool> canRetry, Func<CancellationToken, Task<bool>> round, CancellationToken stop);
}
