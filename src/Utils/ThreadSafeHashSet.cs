using System.Collections;
using System.Collections.Concurrent;

namespace LSP2GXL.Utils;

/// <summary>
/// A thread-safe hash set.
/// </summary>
/// <typeparam name="T">the element type of the set</typeparam>
public class ThreadSafeHashSet<T> : IEnumerable<T> where T : notnull
{
    /// <summary>
    /// Content of the set.
    /// We use a dictionary with a dummy value to simulate a set.
    /// </summary>
    private readonly ConcurrentDictionary<T, bool> content = new();

    /// <summary>
    /// Adds an <paramref name="item"/> to the set.
    /// </summary>
    /// <param name="item">item to be added</param>
    /// <returns>true if the item was added; false if it already existed</returns>
    public bool Add(T item)
    {
        return content.TryAdd(item, true);
    }

    public bool Remove(T item)
    {
        return content.Remove(item, out _);
    }

    public void Clear()
    {
        content.Clear();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>enumerator for the set</returns>
    public IEnumerator GetEnumerator()
    {
        return content.Keys.GetEnumerator();
    }

    /// <summary>
    /// The elements of the set as an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <returns>elements of the set</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return content.Keys.GetEnumerator();
    }

    /// <summary>
    /// The number of elements in the set.
    /// </summary>
    public int Count => content.Count;
}
