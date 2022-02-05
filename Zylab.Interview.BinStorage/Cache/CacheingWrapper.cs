using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Zylab.Interview.BinStorage.Data;
using Zylab.Interview.BinStorage.Dto;

namespace Zylab.Interview.BinStorage.Cache
{
    public class CacheingWrapper : ICacheingWrapper
    {
        const int maxCahceFileDefault = 200000000;
        const int defaultCacheSize = 80000;

        private readonly int _maxCacheFile;
        private readonly int _cacheSize;
        private readonly int _cacheingSlotsCount;

        public static readonly ConcurrentDictionary<string, StreamCache> cache = new ConcurrentDictionary<string, StreamCache>();

        public CacheingWrapper() : this(maxCahceFileDefault)
        {
        }

        public CacheingWrapper(int maxCacheFile) : this(maxCacheFile, defaultCacheSize)
        {
        }

        public CacheingWrapper(int maxCacheFile, int cacheSize)
        {
            _maxCacheFile = maxCacheFile;
            _cacheSize = cacheSize;
            _cacheingSlotsCount = _maxCacheFile / _cacheSize;
        }

        public async Task TryAddStreamDataToCache(string key, byte[] streamData)
        {
            var stremFromCache = TryGetValue(key, out StreamCache value);

            if (!stremFromCache)
            {
                return;
            }

            if (cache.Values.Where(x => x.IsCached).Count() < _cacheingSlotsCount)
            {
                value.IsCached = true;
                value.DataStream = streamData;
                return;
            }

            var minimalViewsCount = cache
                .Select(x => new MinimalViewsDto { Key = x.Key, Views = x.Value.ReadCount });

            string minKey = "";
            int minValue = 0;

            foreach (var item in minimalViewsCount)
            {
                if (item.Views < minValue)
                {
                    minValue = item.Views;
                    minKey = item.Key;
                }
            }

            if (minValue < value.ReadCount)
            {
                if (cache.TryGetValue(minKey, out StreamCache streamCachedata))
                {
                    streamCachedata.DataStream = null;
                    streamCachedata.IsCached = false;

                    if (cache.TryGetValue(key, out StreamCache streamCachedataNew))
                    {
                        value.IsCached = true;
                        value.DataStream = streamData;
                    }
                }
            }
        }

        public bool TryGetValue(string key, out StreamCache value)
            => cache.TryGetValue(key, out value);

        public async Task<bool> TryAdd(string key, StreamCache value)
            => cache.TryAdd(key, value);

        public bool ContainsKey(string key)
            => cache.ContainsKey(key);

        public async Task IncrementWiews(string key)
        {
            //May be locking here
            cache[key].ReadCount++;
        }

    }
}
