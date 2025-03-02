using Not.Injection;

namespace Not.Cache;

public interface ICache<T> : ISingleton
    where T : class
{
    Task<IEnumerable<T>> List();
    Task<T?> Get(int id);
    void Clear();
    
    /// <summary>
    /// Update the state of the entry if found in cache
    /// </summary>
    /// <param name="entry"></param>
    void Update(T entry);

    /// <summary>
    /// Adds an entry to the cache
    /// </summary>
    /// <param name="entry"></param>
    void Add(T entry);

    /// <summary>
    /// Removes an entry from the cache
    /// </summary>
    /// <param name="entry"></param>
    void Delete(T entry);}
