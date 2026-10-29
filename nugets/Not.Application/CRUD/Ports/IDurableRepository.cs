namespace Not.Application.CRUD.Ports;

/// <summary>
/// A repository whose writes are backed up until the remote has answered for good. The remote stays the ultimate storage: the
/// backup holds only what the remote has not confirmed, so that a change survives the remote being slow or failing. Its plain
/// operations are the remote's.
/// </summary>
public interface IDurableRepository<T> : IRepository<T>
{
    /// <summary>
    /// Backs up the item under the id, then asks the remote with it. Returns true when the remote gave a final answer, and then the
    /// backup is removed. When the remote did not answer for good, or failed, the backup stays to be tried again. A backup that
    /// cannot be written throws before the remote is asked.
    /// </summary>
    Task<bool> StoreDurably(Guid id, T item, Func<T, Task<bool>> send);

    /// <summary>Deletes the item at the remote, and removes its backup once the remote has deleted it.</summary>
    Task RemoveDurably(Guid id, T item);

    /// <summary>The backup of the id while there is one, otherwise what the remote has.</summary>
    Task<T?> ReadDurably(Guid id);

    /// <summary>
    /// The backups that match, of every id: what the remote has not answered for good, for the owner the match names. A backup that
    /// is not an item is removed.
    /// </summary>
    Task<IReadOnlyList<T>> ReadPendingDurably(Func<T, bool> match);

    /// <summary>Keeps the backup of the id as the item is now, without asking the remote: for an item that waits and has changed.</summary>
    Task KeepDurably(Guid id, T item);

    /// <summary>Removes the backup of the id without asking the remote: for an item that no longer needs to be sent.</summary>
    Task ForgetDurably(Guid id);
}
