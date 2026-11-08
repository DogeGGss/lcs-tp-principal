using System;
using System.Collections.Generic;
using System.Globalization;

namespace Riftwalker.Localization
{
    [Serializable] public sealed class TranslationEntry { public string key; public string text; }
    [Serializable] public sealed class TranslationFile { public TranslationEntry[] entries; }
    [Serializable] public sealed class LocaleDefinition { public string code; public string name; }
    [Serializable] public sealed class LocaleManifest { public LocaleDefinition[] locales; }

    /// <summary>Catalog rules shared by the Unity player, launcher and validation tools.</summary>
    public sealed class TranslationCatalog
    {
        private readonly Dictionary<string, Dictionary<string, string>> languages =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        public const string DefaultLanguage = "es";

        public void Add(string locale, IEnumerable<TranslationEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(locale)) throw new ArgumentException("A locale is required.", nameof(locale));
            if (!languages.TryGetValue(locale, out var table))
                languages.Add(locale, table = new Dictionary<string, string>(StringComparer.Ordinal));
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.text))
                    throw new ArgumentException("Translation keys and values must be nonempty.", nameof(entries));
                if (table.ContainsKey(entry.key)) throw new ArgumentException("Duplicate translation key: " + entry.key);
                table.Add(entry.key, entry.text);
            }
        }

        public bool Contains(string locale, string key) => locale != null && key != null &&
            languages.TryGetValue(locale, out var table) && table.ContainsKey(key);

        public string GetText(string locale, string key)
        {
            if (key == null) return string.Empty;
            if (locale != null && languages.TryGetValue(locale, out var table) && table.TryGetValue(key, out var text)) return text;
            if (languages.TryGetValue(DefaultLanguage, out var fallback) && fallback.TryGetValue(key, out var source)) return source;
            return "[" + key + "]";
        }

        public string Format(string locale, string key, params object[] args) => FormatText(locale, GetText(locale, key), args);

        public static string FormatText(string locale, string text, params object[] args)
        {
            if (args == null || args.Length == 0) return text;
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(locale ?? DefaultLanguage); }
            catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
            return string.Format(culture, text, args);
        }
    }
}
