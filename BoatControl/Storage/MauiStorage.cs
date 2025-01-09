using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatControl.Storage
{
    using BoatControl.Communication.Storage;
    using System.Text.Json;

    public class MauiStorage : IPersistedStorage
    {
        private Dictionary<string, string> _cache;
        private const string StorageKey = "MauiStorage";

        public MauiStorage()
        {
            var content = Preferences.Default.Get(StorageKey, (string)null);
            _cache = !string.IsNullOrEmpty(content)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(content)
                : new Dictionary<string, string>();
        }

        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            try
            {
                if (_cache.TryGetValue(key, out var val))
                {
                    return JsonSerializer.Deserialize<T>(val);
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void AddOrUpdateValue<T>(string key, T value)
        {
            _cache[key] = JsonSerializer.Serialize(value);
            Preferences.Default.Set(StorageKey, JsonSerializer.Serialize(_cache));
        }

        public void Clear()
        {
            Preferences.Default.Clear();
            _cache.Clear();
        }

        public bool HasValue(string key)
        {
            return _cache.ContainsKey(key);
        }
    }
}
