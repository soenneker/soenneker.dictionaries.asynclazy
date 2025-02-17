[![](https://img.shields.io/nuget/v/soenneker.dictionaries.asynclazy.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.asynclazy/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.asynclazy/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.asynclazy/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.dictionaries.asynclazy.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.asynclazy/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Dictionaries.AsyncLazy
### A thread-safe dictionary that lazily initializes values asynchronously with automatic disposal

A high-performance asynchronous lazy-loading dictionary in C#, designed to ensure only one factory execution per key while supporting concurrent access.

## Installation

```
dotnet add package Soenneker.Dictionaries.AsyncLazy
```

## Usage

### Basic Example

```csharp
var dictionary = new AsyncLazyDictionary<string, string>();

string result = await dictionary.Get("greeting", async () => {
    await Task.Delay(1000, token); // Simulate data fetching
    return "Hello, World!";
});

Console.WriteLine(result); // "Hello, World!"
```

### Removing an Entry

```csharp
await dictionary.Remove("greeting");
```

### Proper Disposal

```csharp
await dictionary.DisposeAsync();
```