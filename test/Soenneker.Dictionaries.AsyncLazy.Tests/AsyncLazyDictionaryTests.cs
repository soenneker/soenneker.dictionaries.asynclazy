using AwesomeAssertions;
using Soenneker.Tests.FixturedUnit;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Soenneker.Dictionaries.AsyncLazy.Tests;

[Collection("Collection")]
public class AsyncLazyDictionaryTests : FixturedUnitTest
{
    private readonly AsyncLazyDictionary<string, int> _dictionary = new();

    public AsyncLazyDictionaryTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }


    [Fact]
    public async Task Get_ShouldReturnStoredValue_WhenCalledMultipleTimes()
    {
        // Arrange
        string key = "test";
        int expectedValue = 42;
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(expectedValue);

        // Act
        int firstResult = await _dictionary.Get(key, factory, CancellationToken);
        int secondResult = await _dictionary.Get(key, factory, CancellationToken);

        // Assert
        firstResult.Should().Be(expectedValue);
        secondResult.Should().Be(expectedValue);
    }

    [Fact]
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
        _ = await _dictionary.Get(key, factory, CancellationToken);
        _ = await _dictionary.Get(key, factory, CancellationToken);

        // Assert
        counter.Should().Be(1);
    }

    [Fact]
    public async Task Remove_ShouldDeleteKey()
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, CancellationToken);

        // Act
        await _dictionary.Remove(key, CancellationToken);
        Func<Task<int>> action = async () => await _dictionary.Get(key, _ => throw new InvalidOperationException(), CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Dispose_ShouldPreventFurtherOperations()
    {
        // Arrange
        string key = "test";
        Func<CancellationToken, ValueTask<int>> factory = _ => new ValueTask<int>(42);
        _ = await _dictionary.Get(key, factory, CancellationToken);

        // Act
        await _dictionary.DisposeAsync();
        Func<Task<int>> action = async () => await _dictionary.Get(key, factory, CancellationToken);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
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
        Task<int> task1 = _dictionary.Get(key, factory, CancellationToken).AsTask();
        Task<int> task2 = _dictionary.Get(key, factory, CancellationToken).AsTask();

        await Task.WhenAll(task1, task2);

        // Assert
        counter.Should().Be(1);
    }
}