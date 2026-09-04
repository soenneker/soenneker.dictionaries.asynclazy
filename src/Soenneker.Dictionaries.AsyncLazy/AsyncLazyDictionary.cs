using Soenneker.Extensions.ValueTask;
using Soenneker.Extensions.Task;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Dictionaries.AsyncLazy.Abstract;

namespace Soenneker.Dictionaries.AsyncLazy;

/// <inheritdoc cref="IAsyncLazyDictionary{TKey, TValue}" />
public sealed class AsyncLazyDictionary<TKey, TValue> : IAsyncLazyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Entry> _entries = new();
    private readonly object _lifecycleLock = new();
    private bool _disposed;

    public async ValueTask<TValue> Get(TKey key, Func<CancellationToken, ValueTask<TValue>> factory, CancellationToken cancellationToken = default)
    {
        Task<TValue> task;

        lock (_lifecycleLock)
        {
            ThrowIfDisposed();

            Entry? candidate = null;
            candidate = new Entry(() => CreateValue(key, candidate!, factory, cancellationToken));
            Entry entry = _entries.GetOrAdd(key, candidate);
            task = entry.Value.Value;
        }

        return await task.WaitAsync(cancellationToken).NoSync();
    }

    private async Task<TValue> CreateValue(TKey key, Entry entry, Func<CancellationToken, ValueTask<TValue>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await factory(cancellationToken).NoSync();
        }
        catch
        {
            lock (_lifecycleLock)
            {
                if (_entries.TryGetValue(key, out Entry? current) && ReferenceEquals(current, entry))
                    _entries.TryRemove(key, out _);
            }

            throw;
        }
    }

    public async ValueTask Remove(TKey key, CancellationToken cancellationToken = default)
    {
        Entry? entry;

        lock (_lifecycleLock)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryRemove(key, out entry);
        }

        if (entry is null || !entry.Value.IsValueCreated)
            return;

        TValue value;

        try
        {
            value = await entry.Value.Value.NoSync();
        }
        catch
        {
            return; // A failed factory has no value to dispose.
        }

        await DisposeValue(value).NoSync();
    }

    public async ValueTask DisposeAsync()
    {
        KeyValuePair<TKey, Entry>[] entries;

        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            entries = _entries.ToArray();
            _entries.Clear();
        }

        for (var i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i].Value;

            if (!entry.Value.IsValueCreated)
                continue;

            TValue value;

            try
            {
                value = await entry.Value.Value.NoSync();
            }
            catch
            {
                continue; // A failed factory has no value to dispose.
            }

            await DisposeValue(value).NoSync();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncLazyDictionary<TKey, TValue>));
    }

    private static async ValueTask DisposeValue(TValue value)
    {
        if (value is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().NoSync();
        else if (value is IDisposable disposable)
            disposable.Dispose();
    }

    private sealed class Entry
    {
        internal readonly Lazy<Task<TValue>> Value;

        internal Entry(Func<Task<TValue>> factory)
        {
            Value = new Lazy<Task<TValue>>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
