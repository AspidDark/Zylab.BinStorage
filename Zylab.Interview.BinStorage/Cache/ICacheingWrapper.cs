using System.IO;
using System.Threading.Tasks;
using Zylab.Interview.BinStorage.Data;

namespace Zylab.Interview.BinStorage.Cache
{
    public interface ICacheingWrapper
    {
        bool ContainsKey(string key);
        Task IncrementWiews(string key);
        Task<bool> TryAdd(string key, StreamCache value);
        Task TryAddStreamDataToCache(string key, byte[] streamData);
        bool TryGetValue(string key, out StreamCache value);
    }
}