#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace TempFileCleaner
{
    public static class ConfigManager
    {
        public static event EventHandler<Exception>? OnError;
        static ConfigData? _data = Load();
        static string _path = string.Empty;
        const string _arrayDelimiter = "|"; // We'll store arrays in a single string attribute

        public static string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_path))
                    _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xml");
                return _path;
            }
        }

        #region [Reading/Writing]
        static ConfigData? Load()
        {
            if (!File.Exists(FilePath))
                return new ConfigData();

            try
            {
                var xml = File.ReadAllText(FilePath);
                using (StringReader stringReader = new StringReader(xml))
                {
                    using (XmlReader xmlReader = XmlReader.Create(stringReader))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(ConfigData));
                        if (serializer.CanDeserialize(xmlReader))
                            return (ConfigData?)serializer.Deserialize(xmlReader);

                        OnError?.Invoke(null, new Exception($"Cannot deserialize XML: \"{xml}\""));
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                if (ex != null && ex.Message.Contains($"{App.GetCurrentNamespace()}.XmlSerializers"))
                    Debug.WriteLine("[WARNING] Ignoring XmlSerializer warning from ConfigManager.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(null, ex);
                Debug.WriteLine($"[ERROR] ConfigManager.Load() ⇒ {ex.Message}");
            }
            return new ConfigData();
        }

        static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var serializer = new XmlSerializer(typeof(ConfigData));
                using (var writer = new StreamWriter(FilePath, false, Encoding.UTF8))
                {
                    serializer.Serialize(writer, _data);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(null, ex);
                Debug.WriteLine($"[ERROR] ConfigManager.Save() ⇒ {ex.Message}");
            }
        }
        #endregion

        #region [Getters]
        public static string? Get(string key, string? defaultValue = null)
        {
            try
            {
                var setting = _data?.Settings.FirstOrDefault(s => s.Key == key);
                return setting != null ? setting.Value : defaultValue;
            }
            catch { return defaultValue; }
        }

        /// <summary>
        /// Generic getter: handles Primitives, Enums, Arrays, and Lists.
        /// </summary>
        public static T? Get<T>(string key, T? defaultValue = default)
        {
            try
            {
                string? raw = Get(key, null);
                if (raw == null) 
                    return defaultValue;

                Type t = typeof(T);

                // Handle Arrays (e.g., int[], string[])
                if (t.IsArray)
                {
                    Type? elementType = t.GetElementType();
                    if (elementType == null) return defaultValue;

                    string[] parts = raw.Split(new[] { _arrayDelimiter }, StringSplitOptions.None);
                    Array array = Array.CreateInstance(elementType, parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        array.SetValue(Convert.ChangeType(parts[i], elementType, CultureInfo.InvariantCulture), i);
                    }
                    return (T)(object)array;
                }

                // Handle Lists (e.g., List<int>, List<string>)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type elementType = t.GetGenericArguments()[0];
                    string[] parts = raw.Split(new[] { _arrayDelimiter }, StringSplitOptions.None);
                    IList list = (IList)Activator.CreateInstance(t)!;
                    foreach (var part in parts)
                    {
                        list.Add(Convert.ChangeType(part, elementType, CultureInfo.InvariantCulture));
                    }
                    return (T)list;
                }

                // Handle Enums
                if (t.IsEnum) return (T)Enum.Parse(t, raw, true);

                // Handle Primitives
                return (T)Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
            }
            catch { return defaultValue; }
        }
        #endregion

        #region [Setters]
        public static void Set(string key, string value, bool saveAfterUpdate = true)
        {
            try
            {
                var setting = _data?.Settings.FirstOrDefault(s => s.Key == key);
                if (setting == null)
                    _data?.Settings.Add(new Setting { Key = key, Value = value, TypeName = typeof(string).AssemblyQualifiedName });
                else
                    setting.Value = value;

                if (saveAfterUpdate)
                    Save();
            }
            catch { }
        }

        /// <summary>
        /// Robust Generic Setter: Automatically flattens collections into delimited strings.
        /// </summary>
        public static void Set<T>(string key, T value, bool saveAfterUpdate = true)
        {
            string? strValue;
            try
            {
                if (value == null)
                    strValue = null;
                else if (value is IEnumerable enumerable && !(value is string)) // Check if it's a collection (but not a string)
                {
                    var list = new List<string>();
                    foreach (var item in enumerable)
                    {
                        // Use CultureInfo.InvariantCulture for numbers/dates to avoid "1,5" vs "1.5" issues
                        list.Add(item is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : item.ToString() ?? "");
                    }
                    strValue = string.Join(_arrayDelimiter, list);
                }
                else if (value is IFormattable f)
                    strValue = f.ToString(null, CultureInfo.InvariantCulture);
                else
                    strValue = $"{value}";

                var setting = _data?.Settings.FirstOrDefault(s => s.Key == key);
                if (setting == null)
                    _data?.Settings.Add(new Setting { Key = key, Value = strValue, TypeName = typeof(T).Name });
                else
                    setting.Value = strValue;

                if (saveAfterUpdate)
                    Save();
            }
            catch { }
        }
        #endregion

        #region [Delete]
        /// <summary>
        /// Removes a specific setting by key.
        /// </summary>
        /// <returns>true if the key was found and removed, otherwise false</returns>
        public static bool Remove(string key, bool saveAfterUpdate = true)
        {
            try
            {
                var setting = _data?.Settings.FirstOrDefault(s => s.Key == key);
                if (setting != null)
                {
                    _data?.Settings.Remove(setting);
                    if (saveAfterUpdate)
                        Save();
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Clears all settings from the current config.
        /// </summary>
        public static void ClearAll(bool saveAfterUpdate = true)
        {
            try
            {
                _data?.Settings.Clear();
                if (saveAfterUpdate) 
                    Save();
            }
            catch { }
        }
        #endregion
    }

    #region [XML Config Model]
    [XmlRoot("configuration")]
    public class ConfigData
    {
        [XmlElement("add")]
        public List<Setting> Settings { get; set; } = new List<Setting>();
    }

    public class Setting
    {
        [XmlAttribute("key")]
        public string? Key { get; set; }

        [XmlAttribute("value")]
        public string? Value { get; set; }

        [XmlAttribute("type")]
        public string? TypeName { get; set; }
    }
    #endregion

}
