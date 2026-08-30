using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Dictionaries.AsyncLazy.Abstract
{
    /// <summary>
    /// Defines a thread-safe, asynchronous, lazy-loaded dictionary that shares one in-flight factory per key and caches successful values.
    /// </summary>
    public interface IAsyncLazyDictionary<TKey, TValue> : IAsyncDisposable, IDisposable where TKey : notnull
    {
        /// <summary>
        /// Retrieves the value associated with the specified key.
        /// If the key does not exist, one caller's factory creates it asynchronously while concurrent callers await the same operation.
        /// </summary>
        /// <param name="key">The unique key to retrieve or create the value.</param>
        /// <param name="factory">A factory function that generates a new value asynchronously if the key is not present.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>The value associated with the key.</returns>
        ValueTask<TValue> Get(TKey key, Func<CancellationToken, ValueTask<TValue>> factory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the value associated with the specified key and disposes a successfully materialized value.
        /// If the key is not found, no action is taken.
        /// </summary>
        /// <param name="key">Key used to locate the target entry.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that completes when the remove operation is complete.</returns>
        ValueTask Remove(TKey key, CancellationToken cancellationToken = default);
    }
}
