using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Dictionaries.AsyncLazy.Abstract
{
    /// <summary>
    /// Defines a thread-safe, asynchronous, and lazy-loaded dictionary that ensures a single execution of the factory function per key.
    /// </summary>
    public interface IAsyncLazyDictionary<TKey, TValue> : IAsyncDisposable, IDisposable where TKey : notnull
    {
        /// <summary>
        /// Retrieves the value associated with the specified key.
        /// If the key does not exist, the provided factory function is invoked (only once) to create it asynchronously.
        /// </summary>
        /// <param name="key">The unique key to retrieve or create the value.</param>
        /// <param name="factory">A factory function that generates a new value asynchronously if the key is not present.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>The value associated with the key.</returns>
        ValueTask<TValue> Get(TKey key, Func<CancellationToken, ValueTask<TValue>> factory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the value associated with the specified key.
        /// If the key is not found, no action is taken.
        /// </summary>
        /// <param name="key">The key whose value should be removed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        ValueTask Remove(TKey key, CancellationToken cancellationToken = default);
    }
}