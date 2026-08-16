using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

public class CacheService
{
    private readonly IMemoryCache _cache;

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    // Get or create cache data based on a key
    public T GetOrCreate<T>(string key, Func<T> createItem, TimeSpan cacheDuration)
    {
        // Try to get the cache entry
        if (!_cache.TryGetValue(key, out T cacheEntry))
        {
            // Item not in cache, so get data via the provided delegate
            cacheEntry = createItem();

            // Set cache options
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(cacheDuration);

            // Set the item in cache
            _cache.Set(key, cacheEntry, cacheEntryOptions);
        }

        return cacheEntry;
    }

    // Get or create cache data based on a key asynchronously
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> createItem, TimeSpan cacheDuration)
    {
        // Try to get the cache entry
        if (!_cache.TryGetValue(key, out T cacheEntry))
        {
            // Item not in cache, so get data via the provided async delegate
            cacheEntry = await createItem();

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(cacheDuration);
            _cache.Set(key, cacheEntry, cacheEntryOptions);
        }

        return cacheEntry;
    }



}
