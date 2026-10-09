using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

/// <summary>
/// Dictionary cache keyed by <see cref="CacheSettings.CacheItemName"/>. Ignores expiry and dependencies.
/// </summary>
internal sealed class FakeCache : IProgressiveCache, IStatsCacheInvalidator
{
    private readonly Dictionary<string, object?> items = new(StringComparer.OrdinalIgnoreCase);

    public List<CacheSettings> Settings { get; } = [];

    /// <summary>Cached items.</summary>
    public int Count => items.Count;

    public TData Load<TData>(Func<CacheSettings, TData> loadDataFunc, CacheSettings settings) =>
        throw new NotSupportedException();

    public Task<TData> LoadAsync<TData>(Func<CacheSettings, Task<TData>> loadDataFuncAsync, CacheSettings settings) =>
        LoadAsync((s, _) => loadDataFuncAsync(s), settings, CancellationToken.None);

    public async Task<TData> LoadAsync<TData>(Func<CacheSettings, CancellationToken, Task<TData>> loadDataFuncAsync, CacheSettings settings, CancellationToken cancellationToken)
    {
        Settings.Add(settings);

        if (items.TryGetValue(settings.CacheItemName, out object? cached))
        {
            return (TData)cached!;
        }

        var data = await loadDataFuncAsync(settings, cancellationToken);
        items[settings.CacheItemName] = data;
        return data;
    }

    public void Remove(CacheSettings settings) => items.Remove(settings.CacheItemName);
}

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
