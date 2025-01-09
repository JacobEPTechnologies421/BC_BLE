using System;
using System.Collections.Generic;
using System.Text;

namespace BoatControl.Communication.Storage
{
    public interface IPersistedStorage
    {
        /// <summary>
        /// Gets the current value or the default that you specify.
        /// </summary>
        /// <typeparam name="T">Vaue of t (bool, int, float, long, string)</typeparam>
        /// <param name="key">Key for settings</param>
        /// <param name="defaultValue">default value if not set</param>
        /// <returns>Value or default</returns>
        T GetValueOrDefault<T>(string key, T defaultValue);

        /// <summary>Adds or updates the value</summary>
        /// <param name="key">Key for settting</param>
        /// <param name="value">Value to set</param>
        /// <returns>True of was added or updated and you need to save it.</returns>
        void AddOrUpdateValue<T>(string key, T value);

        /// <summary>
        /// Check if value exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool HasValue(string key);

        /// <summary>
        /// Clear all data (used for log off)
        /// </summary>
        void Clear();
    }
}
