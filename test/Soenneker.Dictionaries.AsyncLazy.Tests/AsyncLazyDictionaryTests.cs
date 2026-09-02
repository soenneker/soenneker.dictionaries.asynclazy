using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Dictionaries.AsyncLazy.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class AsyncLazyDictionaryTests : HostedUnitTest
{
    private readonly AsyncLazyDictionary<string, int> _dictionary = new();

    public AsyncLazyDictionaryTests(Host host) : base(host)
    {
    }

    [Test]
    public async Task Get_ShouldReturnStoredValue_WhenCalledMultipleTimes(CancellationToken cancellationToken)
    {
        // Arrange
        string key = "test";
        int expectedValue = 42;
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(expectedValue);

        // Act
        int firstResult = await _dictionary.Get(key, factory, cancellationToken);
        int secondResult = await _dictionary.Get(key, factory, cancellationToken);

        // Assert
        firstResult.Should().Be(expectedValue);
        secondResult.Should().Be(expectedValue);
    }

    [Test]
    public async Task Get_ShouldCallFactoryOnlyOnce_ForSameKey(CancellationToken cancellationToken)
    {
        // Arrange
        string key = "test";
        int counter = 0;
        Func<CancellationToken, ValueTask<int>> factory = _ =>
        {
            counter++;
            return new ValueTask<int>(42);
        };

        // Act
        _ = await _dictionary.Get(key, factory, cancellationToken);
        _ = await _dictionary.Get(key, factory, cancellationToken);

        // Assert
        counter.Should().Be(1);
    }

    [Test]
    public async Task Remove_ShouldDeleteKey(CancellationToken cancellationToken)
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, cancellationToken);

        // Act
        await _dictionary.Remove(key, cancellationToken);
        Func<Task<int>> action = async () => await _dictionary.Get(key, _ => throw new InvalidOperationException(), cancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Dispose_ShouldPreventFurtherOperations(CancellationToken cancellationToken)
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, cancellationToken);

        // Act
        await _dictionary.DisposeAsync();
        Func<Task<int>> action = async () => await _dictionary.Get(key, factory, cancellationToken);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task Get_ShouldNotCallFactoryTwice_IfConcurrentCallsAreMade(CancellationToken cancellationToken)
    {
        // Arrange
        string key = "test";
        int counter = 0;
        Func<CancellationToken, ValueTask<int>> factory = _ =>
        {
            Interlocked.Increment(ref counter);
            return new ValueTask<int>(Task.Delay(100).ContinueWith(_ => 42));
        };

        // Act
        Task<int> task1 = _dictionary.Get(key, factory, cancellationToken).AsTask();
        Task<int> task2 = _dictionary.Get(key, factory, cancellationToken).AsTask();

        await Task.WhenAll(task1, task2);

        // Assert
        counter.Should().Be(1);
    }

    [Test]
    public async Task Get_ShouldInitializeDifferentKeysConcurrently(CancellationToken cancellationToken)
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> first = _dictionary.Get("first", async _ =>
        {
            firstEntered.SetResult();
            await release.Task;
            return 1;
        }, cancellationToken).AsTask();

        Task<int> second = _dictionary.Get("second", async _ =>
        {
            secondEntered.SetResult();
            await release.Task;
            return 2;
        }, cancellationToken).AsTask();

        await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult();

        (await first).Should().Be(1);
        (await second).Should().Be(2);
    }

    [Test]
    public async Task Remove_ShouldDisposeMaterializedValue(CancellationToken cancellationToken)
    {
        var dictionary = new AsyncLazyDictionary<string, DisposableValue>();
        var value = new DisposableValue();

        _ = await dictionary.Get("value", _ => new ValueTask<DisposableValue>(value), cancellationToken: cancellationToken);
        await dictionary.Remove("value", cancellationToken: cancellationToken);

        value.Disposed.Should().BeTrue();
        await dictionary.DisposeAsync();
    }

    private sealed class DisposableValue : IDisposable
    {
        internal bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
