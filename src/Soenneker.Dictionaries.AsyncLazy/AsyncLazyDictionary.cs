using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Locks;
using Soenneker.Atomics.ValueBools;
using Soenneker.Dictionaries.AsyncLazy.Abstract;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Dictionaries.AsyncLazy;

/// <inheritdoc cref="IAsyncLazyDictionary{TKey,TValue}"/>
public sealed class AsyncLazyDictionary<TKey, TValue> : IAsyncLazyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _dict = new();
    private readonly ConcurrentDictionary<TKey, ValueTask<TValue>> _valueTaskDict = new();
    private readonly AsyncLock _lock = new();

    private ValueAtomicBool _disposed;

    public async ValueTask<TValue> Get(TKey key, Func<CancellationToken, ValueTask<TValue>> factory, CancellationToken cancellationToken = default)
    {
        if (_disposed.Read())
            throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

        // Fast path: value already materialized
        if (_dict.TryGetValue(key, out TValue? existing))
            return existing;

        // Fast path: in-flight ValueTask already exists
        if (_valueTaskDict.TryGetValue(key, out ValueTask<TValue> inflight))
            return await inflight.NoSync();

        while (true)
        {
            if (_disposed.Read())
                throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

            ValueTask<TValue> created = CreateValueTask(factory, key, cancellationToken);

            if (_valueTaskDict.TryAdd(key, created))
                return await created.NoSync();

            if (_valueTaskDict.TryGetValue(key, out inflight))
                return await inflight.NoSync();
        }
    }

    private async ValueTask<TValue> CreateValueTask(Func<CancellationToken, ValueTask<TValue>> factory, TKey key, CancellationToken cancellationToken)
    {
        using (await _lock.Lock(cancellationToken)
                          .NoSync())
        {
            if (_disposed.Read())
                throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

            if (_dict.TryGetValue(key, out TValue? existing))
                return existing;

            try
            {
                TValue value = await factory(cancellationToken)
                    .NoSync();
                _dict[key] = value;
                return value;
            }
            finally
            {
                _valueTaskDict.TryRemove(key, out _);
            }
        }
    }

    public ValueTask Remove(TKey key, CancellationToken cancellationToken = default)
    {
        if (_disposed.Read())
            throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));

        _dict.TryRemove(key, out _);
        _valueTaskDict.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Ensure dispose runs exactly once
        if (!_disposed.TrySetTrue())
            return;

        foreach (KeyValuePair<TKey, TValue> kvp in _dict)
        {
            if (kvp.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync()
                                     .NoSync();
            else if (kvp.Value is IDisposable disposable)
                disposable.Dispose();
        }

        _dict.Clear();
        _valueTaskDict.Clear();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}