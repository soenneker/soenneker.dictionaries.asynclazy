[![](https://img.shields.io/nuget/v/soenneker.dictionaries.asynclazy.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.asynclazy/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.asynclazy/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.asynclazy/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.dictionaries.asynclazy.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.asynclazy/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.asynclazy/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.asynclazy/actions/workflows/codeql.yml)

# Soenneker.Dictionaries.AsyncLazy

A concurrent dictionary that runs at most one asynchronous value factory per key, caches successful results, and disposes removed values it owns.

## Installation

```bash
dotnet add package Soenneker.Dictionaries.AsyncLazy
```

## Usage

```csharp
using Soenneker.Dictionaries.AsyncLazy;

await using var clients = new AsyncLazyDictionary<string, ApiClient>();

ApiClient client = await clients.Get(
    "billing",
    async cancellationToken =>
    {
        ApiClient created = await ApiClient.Connect(cancellationToken);
        return created;
    },
    cancellationToken);
```

Concurrent callers for `"billing"` await the same factory task and receive the same instance. Factories for different keys run concurrently.

The factory and its cancellation token come from the call that wins creation for the key. Cancellation of that factory cancels the shared initialization. Other callers can cancel their own wait without removing an initialization that is still running.

## Failures and retries

A faulted or canceled factory is removed from the dictionary. The failing callers observe the original exception, and a later `Get` can run a new factory:

```csharp
ApiClient client = await clients.Get("billing", ConnectWithRetryPolicy, cancellationToken);
```

The dictionary does not add retries itself; put retry policy inside the factory when appropriate.

## Removal and disposal

```csharp
await clients.Remove("billing", cancellationToken);
```

`Remove` evicts the entry. If initialization has started, removal waits for it to finish and then disposes the value. `IAsyncDisposable` is preferred over `IDisposable`. Coordinate removal with active consumers; an object should not be used after its entry is removed.

Disposing the dictionary prevents new operations, waits for in-flight factories, and disposes every successfully materialized cached value. Factories that return shared objects should wrap them in a non-owning value or avoid this dictionary, because cached values are treated as dictionary-owned.
