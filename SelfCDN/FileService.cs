using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace SelfCDN
{
    record FilesListCache(DateTime LastWriteTime, List<string> Files);

    public class FileService
    {
        private const string CacheKey = nameof(FilesListCache);

        private readonly string _mediaRootPath;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(3);

        public FileService(string mediaRootPath, IMemoryCache memoryCache)
        {
            _mediaRootPath = mediaRootPath;
            _cache = memoryCache;
        }

        public List<string> GetVideoFiles()
        {
            if (!Directory.Exists(ModInit.StoragePath))
            {
                return new List<string>();
            }

            if (!_cache.TryGetValue(CacheKey, out FilesListCache cacheEntry))
            {
                cacheEntry = ScanVideoFiles();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                };

                _cache.Set(CacheKey, cacheEntry, cacheOptions);
            }
            else
            {
                DateTime currentLastWriteTime = Directory.GetLastWriteTime(_mediaRootPath);
                if (currentLastWriteTime > cacheEntry.LastWriteTime)
                {
                    cacheEntry = ScanVideoFiles();

                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _cacheDuration
                    };

                    _cache.Set(CacheKey, cacheEntry, cacheOptions);
                }
            }

            return new List<string>(cacheEntry.Files);
        }

        private FilesListCache ScanVideoFiles()
        {
            var files = ModInit.VideoExtensions
                .SelectMany(ext => Directory.EnumerateFiles(_mediaRootPath, $"*.{ext.TrimStart('.')}", SearchOption.AllDirectories))
                .Distinct()
                .ToList();

            return new FilesListCache(Directory.GetLastWriteTime(_mediaRootPath), files);
        }
    }
}
