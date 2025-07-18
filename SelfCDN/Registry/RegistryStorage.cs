using SelfCdn.Registry.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SelfCDN.Registry
{
    public class RegistryStorage
    {
        private Dictionary<string, List<RegistryMediaItem>> _recognized = new();
        private List<string> _unrecognized = new();
        private List<string> _ignored = new();

        private readonly ReaderWriterLockSlim _lockRecognized = new();
        private readonly ReaderWriterLockSlim _lockUnrecognized = new();
        private readonly ReaderWriterLockSlim _lockIgnored = new();

        // Recognized collection
        [JsonPropertyName(nameof(Recognized))]
        public Dictionary<string, List<RegistryMediaItem>> Recognized
        {
            get { return WithReadLock(_lockRecognized, () => new Dictionary<string, List<RegistryMediaItem>>(_recognized)); }
            set
            {
                WithWriteLock(_lockRecognized, () =>
                {
                    _recognized.Clear();
                    foreach (var kv in value)
                    {
                        _recognized[kv.Key] = kv.Value;
                    }
                });
            }
        }

        public void AddRecognized(long id, string type, RegistryMediaItem item) => WithWriteLock(
            _lockRecognized,
            () =>
            {
                var key = $"{type.ToLower()}:{id}";

                var isExists = _recognized.TryGetValue(key, out var list);

                if (isExists)
                {
                    list.Add(item);
                }
                else
                {
                    list = new List<RegistryMediaItem> { item };
                    _recognized.Add(key, list);
                }
            });

        public IReadOnlyList<RegistryMediaItem> GetRecognized(long id, string type) => WithReadLock(
            _lockRecognized,
            () => _recognized.TryGetValue($"{type.ToLower()}:{id}", out var list)
                ? list.ToList()
                : new List<RegistryMediaItem>());

        public bool RemoveRecognized(long id, string type, RegistryMediaItem item) =>
            RemoveRecognized($"{type.ToLower()}:{id}", item);

        public bool RemoveRecognized(string key, RegistryMediaItem item) => WithWriteLock(
            _lockRecognized,
            () =>
            {
                var isExist = _recognized.TryGetValue(key, out var list);
                if (!isExist)
                {
                    return false;
                }

                return list.Remove(item);
            });

        [JsonIgnore]
        public IReadOnlyList<string> Unrecognized => WithReadLock(_lockUnrecognized, () => _unrecognized.ToList());

        [JsonPropertyName(nameof(Unrecognized))]
        public List<string> SerializableUnrecognized
        {
            get => Unrecognized.ToList();
            set => WithWriteLock(
                _lockUnrecognized,
                () =>
                {
                    _unrecognized.Clear();
                    _unrecognized.AddRange(value);
                });
        }

        public void AddUnrecognized(string item) => WithWriteLock(_lockUnrecognized, () => _unrecognized.Add(item));
        public bool RemoveUnrecognized(string item) => WithWriteLock(_lockUnrecognized, () => _unrecognized.Remove(item));

        // Ignored collection
        [JsonIgnore]
        public IReadOnlyList<string> Ignored => WithReadLock(_lockIgnored, () => _ignored.ToList());

        [JsonPropertyName(nameof(Ignored))]
        public List<string> SerializableIgnored
        {
            get => Ignored.ToList();
            set => WithWriteLock(
                _lockIgnored,
                () =>
                {
                    _ignored.Clear();
                    _ignored.AddRange(value);
                });
        }

        public void AddIgnored(string item) => WithWriteLock(_lockIgnored, () => _ignored.Add(item));
        public bool RemoveIgnored(string item) => WithWriteLock(_lockIgnored, () => _ignored.Remove(item));

        public async Task LoadAsync(string filePath)
        {
            ConsoleLogger.Log("[RegistryStorage] start load file");
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
            {
                ConsoleLogger.Log($"Registry storage file not found: {filePath}");
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);

            try
            {
                var instance = JsonSerializer.Deserialize<RegistryStorage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });


                if (instance != null)
                {
                    _recognized = instance.Recognized;
                    _unrecognized = instance.Unrecognized.ToList();
                    _ignored = instance.Ignored.ToList();


                    ConsoleLogger.Log($"[RegistryStorage] file loaded. " +
                                      $"Recognized: {_recognized.Count}, " +
                                      $"Unrecognized: {_unrecognized.Count}, " +
                                      $"Ignored: {Ignored.Count}");
                }
                else
                {
                    ConsoleLogger.Log("[RegistryStorage] error during loading");
                }

            }
            catch (Exception ex)
            {
                ConsoleLogger.Log(ex.Message);
            }
        }

        public async Task SaveAsync(string filePath)
        {
            ConsoleLogger.Log("[RegistryStorage] start save file");

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
            };

            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, this, jsonOptions);

            ConsoleLogger.Log("[RegistryStorage] file saved");
        }

        public void PruneMissedFiles()
        {
            ConsoleLogger.Log("[RegistryStorage] start prune missed files");

            WithWriteLock(_lockRecognized, () =>
            {
                foreach (var entry in _recognized)
                {
                    entry.Value?.RemoveAll(r => !File.Exists(r.FilePath));
                }

                foreach (var entry in _recognized
                             .Where(kv => kv.Value.Count == 0)
                             .ToList())
                {
                    _recognized.Remove(entry.Key);
                }

                ConsoleLogger.Log("[RegistryStorage] finished prune missed recognized files");
            });

            WithWriteLock(_lockUnrecognized, () =>
            {
                _unrecognized.RemoveAll(f => !File.Exists(f));
            });

            ConsoleLogger.Log("[RegistryStorage] finished prune missed unrecognized files");

            WithWriteLock(_lockIgnored, () =>
            {
                _ignored.RemoveAll(f => !File.Exists(f));
            });

            ConsoleLogger.Log("[RegistryStorage] finished prune missed ignored files");

            ConsoleLogger.Log("[RegistryStorage] finish prune missed files");
        }

        private T WithReadLock<T>(ReaderWriterLockSlim lockObj, Func<T> func)
        {
            lockObj.EnterReadLock();
            try
            {
                return func();
            }
            finally
            {
                lockObj.ExitReadLock();
            }
        }

        private void WithWriteLock(ReaderWriterLockSlim lockObj, Action action)
        {
            lockObj.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                lockObj.ExitWriteLock();
            }
        }

        private T WithWriteLock<T>(ReaderWriterLockSlim lockObj, Func<T> func)
        {
            lockObj.EnterWriteLock();
            try
            {
                return func();
            }
            finally
            {
                lockObj.ExitWriteLock();
            }
        }
    }
}
