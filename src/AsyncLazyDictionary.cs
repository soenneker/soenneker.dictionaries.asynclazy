using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Locks;
using Soenneker.Dictionaries.AsyncLazy.Abstract;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Dictionaries.AsyncLazy;

/// <inheritdoc cref="IAsyncLazyDictionary{TKey, TValue}"/>
public sealed class AsyncLazyDictionary<TKey, TValue> : IAsyncLazyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _dict = new();
    private readonly ConcurrentDictionary<TKey, ValueTask<TValue>> _valueTaskDict = new();
    private readonly AsyncLock _lock = new();
    private bool _disposed;

    public async ValueTask<TValue> Get(TKey key, Func<CancellationToken, ValueTask<TValue>> factory, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

        // First check before locking
        if (_dict.TryGetValue(key, out TValue? existingValue))
            return existingValue;

        // Ensure only one factory execution per key by storing in a temporary ValueTask dictionary
        ValueTask<TValue> newTask = _valueTaskDict.GetOrAdd(key, _ => CreateValueTask(factory, key, cancellationToken));

        return await newTask.ConfigureAwait(false);
    }

    private async ValueTask<TValue> CreateValueTask(Func<CancellationToken, ValueTask<TValue>> factory, TKey key, CancellationToken cancellationToken)
    {
        using (await _lock.Lock(cancellationToken).NoSync())
        {
            // Second check inside lock to prevent duplicate execution
            if (_dict.TryGetValue(key, out TValue? existingValue))
                return existingValue;

            TValue value = await factory(cancellationToken).NoSync();

            _dict[key] = value; // Store the final value
            _valueTaskDict.TryRemove(key, out _); // Clean up the task dictionary
            return value;
        }
    }

    public async ValueTask Remove(TKey key, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

        using (await _lock.Lock(cancellationToken).NoSync())
        {
            _dict.TryRemove(key, out _);
            _valueTaskDict.TryRemove(key, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (KeyValuePair<TKey, TValue> kvp in _dict)
        {
            if (kvp.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().NoSync();

            else if (kvp.Value is IDisposable disposable)
                disposable.Dispose();
        }

        _dict.Clear();
        _valueTaskDict.Clear();
    }
}