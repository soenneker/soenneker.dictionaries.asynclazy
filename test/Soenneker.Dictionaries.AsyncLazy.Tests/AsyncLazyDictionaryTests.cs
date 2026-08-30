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
    public async Task Get_ShouldReturnStoredValue_WhenCalledMultipleTimes()
    {
        // Arrange
        string key = "test";
        int expectedValue = 42;
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(expectedValue);

        // Act
        int firstResult = await _dictionary.Get(key, factory, CancellationToken.None);
        int secondResult = await _dictionary.Get(key, factory, CancellationToken.None);

        // Assert
        firstResult.Should().Be(expectedValue);
        secondResult.Should().Be(expectedValue);
    }

    [Test]
    public async Task Get_ShouldCallFactoryOnlyOnce_ForSameKey()
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
        _ = await _dictionary.Get(key, factory, CancellationToken.None);
        _ = await _dictionary.Get(key, factory, CancellationToken.None);

        // Assert
        counter.Should().Be(1);
    }

    [Test]
    public async Task Remove_ShouldDeleteKey()
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, CancellationToken.None);

        // Act
        await _dictionary.Remove(key, CancellationToken.None);
        Func<Task<int>> action = async () => await _dictionary.Get(key, _ => throw new InvalidOperationException(), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Dispose_ShouldPreventFurtherOperations()
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, CancellationToken.None);

        // Act
        await _dictionary.DisposeAsync();
        Func<Task<int>> action = async () => await _dictionary.Get(key, factory, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task Get_ShouldNotCallFactoryTwice_IfConcurrentCallsAreMade()
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
        Task<int> task1 = _dictionary.Get(key, factory, CancellationToken.None).AsTask();
        Task<int> task2 = _dictionary.Get(key, factory, CancellationToken.None).AsTask();

        await Task.WhenAll(task1, task2);

        // Assert
        counter.Should().Be(1);
    }

    [Test]
    public async Task Get_ShouldInitializeDifferentKeysConcurrently()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> first = _dictionary.Get("first", async _ =>
        {
            firstEntered.SetResult();
            await release.Task;
            return 1;
        }).AsTask();

        Task<int> second = _dictionary.Get("second", async _ =>
        {
            secondEntered.SetResult();
            await release.Task;
            return 2;
        }).AsTask();

        await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult();

        (await first).Should().Be(1);
        (await second).Should().Be(2);
    }

    [Test]
    public async Task Remove_ShouldDisposeMaterializedValue()
    {
        var dictionary = new AsyncLazyDictionary<string, DisposableValue>();
        var value = new DisposableValue();

        _ = await dictionary.Get("value", _ => new ValueTask<DisposableValue>(value));
        await dictionary.Remove("value");

        value.Disposed.Should().BeTrue();
        await dictionary.DisposeAsync();
    }

    private sealed class DisposableValue : IDisposable
    {
        internal bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
