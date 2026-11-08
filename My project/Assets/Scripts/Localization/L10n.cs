using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Riftwalker.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>Player-facing text. Locale state is owned by Unity Localization.</summary>
public static class L10n
{
    public const string TableName = "Riftwalker";
    private static TranslationCatalog catalog;
    private static readonly Dictionary<string, string> sourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string,List<string>> sourceCandidates = new Dictionary<string,List<string>>(StringComparer.Ordinal);
    private static readonly Dictionary<string,string> authoredKeys = new Dictionary<string,string>(StringComparer.Ordinal);
    private static LocaleDefinition[] languages;
    private static string startupLanguage;
    private static bool subscribed;
    public static event Action LanguageChanged;
    // Los tests (ModoPruebas) usan su propio archivo, así no cambian el idioma elegido por el jugador.
    public static string PreferencePath => ModoPruebas.Activo
        ? Path.Combine(Path.GetTempPath(), "riftwalker-pruebas-" + LanguagePreference.FileName)
        : Path.Combine(Application.persistentDataPath, LanguagePreference.FileName);
    public static IReadOnlyList<LocaleDefinition> Languages { get { EnsureCatalog(); return languages; } }
    public static string LanguageCode
    {
        get
        {
            EnsureCatalog();
            // Do not force a synchronous initialization while another startup operation is running.
            if (!LocalizationSettings.InitializationOperation.IsDone) return startupLanguage;
            var selected = LocalizationSettings.SelectedLocale;
            return selected != null ? selected.Identifier.Code : startupLanguage;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        if (subscribed) LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        subscribed = false; catalog = null; languages = null; sourceKeys.Clear(); sourceCandidates.Clear(); authoredKeys.Clear(); LanguageChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Start()
    {
        EnsureCatalog();
        var host = new GameObject("Localization").AddComponent<LanguageStartup>();
        UnityEngine.Object.DontDestroyOnLoad(host.gameObject);
    }

    private static void EnsureCatalog()
    {
        // Subscribed here too: in edit mode (tests, editor tools) Start() never runs.
        if (!subscribed) { LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; subscribed = true; }
        if (catalog != null) return;
        catalog = new TranslationCatalog();
        var manifest = Resources.Load<TextAsset>("Localization/locales");
        languages = manifest != null ? JsonUtility.FromJson<LocaleManifest>(manifest.text).locales : null;
        if (languages == null || languages.Length == 0)
            languages = new[] { new LocaleDefinition { code = "es", name = "Español" } };
        startupLanguage = LanguagePreference.Load(PreferencePath);
        bool supported = false;
        foreach (var locale in languages)
        {
            if (string.Equals(locale.code,startupLanguage,StringComparison.OrdinalIgnoreCase)) { supported = true; startupLanguage = locale.code; }
            foreach (var asset in SortedCatalogs(locale.code))
            {
                var file = JsonUtility.FromJson<TranslationFile>(asset.text);
                catalog.Add(locale.code, file.entries);
                if (locale.code != TranslationCatalog.DefaultLanguage || file.entries == null) continue;
                foreach (var entry in file.entries)
                    {
                        if (!sourceKeys.ContainsKey(entry.text)) sourceKeys.Add(entry.text, entry.key);
                        if (!sourceCandidates.TryGetValue(entry.text,out var candidates)) sourceCandidates.Add(entry.text,candidates = new List<string>());
                        candidates.Add(entry.key);
                        if (entry.key.StartsWith("ui.scene.",StringComparison.Ordinal)) authoredKeys[entry.text] = entry.key;
                    }
            }
        }
        if (!supported) startupLanguage = TranslationCatalog.DefaultLanguage;
    }

    private static TextAsset[] SortedCatalogs(string locale)
    {
        var assets = Resources.LoadAll<TextAsset>("Localization/" + locale);
        Array.Sort(assets,(a,b) => string.CompareOrdinal(a.name,b.name));
        return assets;
    }

    public static bool TrySourceKey(string source,out string key,string domain = "gameplay.")
    {
        EnsureCatalog(); key = null;
        if (source == null || !sourceCandidates.TryGetValue(source,out var candidates)) return false;
        List<string> scoped = candidates.FindAll(k => !string.IsNullOrEmpty(domain) && k.StartsWith(domain,StringComparison.Ordinal));
        var choices = scoped.Count > 0 ? scoped : candidates;
        string chosen = choices[0];
        foreach (string candidate in choices)
            foreach (var language in languages)
                if (catalog.GetText(language.code,chosen) != catalog.GetText(language.code,candidate)) return false;
        key = chosen; return true;
    }

    public static bool TryAuthoredKey(string source,out string key) { EnsureCatalog(); return authoredKeys.TryGetValue(source,out key); }

    public static string Source(string spanishText,string domain = "gameplay.") => TrySourceKey(spanishText,out var key,domain) ? T(key) : spanishText;

    public static string T(string key, params object[] args)
    {
        EnsureCatalog();
        string raw = catalog.GetText(LanguageCode, key);
        if (LocalizationSettings.InitializationOperation.IsDone && LocalizationSettings.InitializationOperation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue)) raw = entry.LocalizedValue;
        }
        return TranslationCatalog.FormatText(LanguageCode, raw, args);
    }

    public static string Plural(string key, int count, params object[] args)
    {
        var values = new object[(args?.Length ?? 0) + 1]; values[0] = count;
        if (args != null) Array.Copy(args, 0, values, 1, args.Length);
        return T(key + (count == 1 ? ".one" : ".other"), values);
    }

    public static void SetLanguage(string code)
    {
        EnsureCatalog();
        foreach (var language in languages)
        {
            if (!string.Equals(language.code, code, StringComparison.OrdinalIgnoreCase)) continue;
            LanguagePreference.Save(PreferencePath, language.code);
            startupLanguage = language.code;
            var locale = LocalizationSettings.InitializationOperation.IsDone ? LocalizationSettings.AvailableLocales.GetLocale(language.code) : null;
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
            else LanguageChanged?.Invoke(); // without Unity locales (catalogs not synchronized yet) the JSON catalog still switches
            // Startup reads the shared preference again after the initialization operation completes.
            return;
        }
        throw new ArgumentException("Unsupported language: " + code, nameof(code));
    }

    private static void OnLocaleChanged(UnityEngine.Localization.Locale locale) => LanguageChanged?.Invoke();

    public static void Bind(TMP_Text target, string key, params object[] args)
    {
        if (target != null) (target.GetComponent<LocalizedGameText>() ?? target.gameObject.AddComponent<LocalizedGameText>()).Configure(target, key, args);
    }
    public static void Bind(UnityEngine.UI.Text target, string key, params object[] args)
    {
        if (target != null) (target.GetComponent<LocalizedGameText>() ?? target.gameObject.AddComponent<LocalizedGameText>()).Configure(target, key, args);
    }
    public static void BindSource(TMP_Text target) { if (target != null) BindSource(target, target.text); }
    public static void BindSource(TMP_Text target, string source,string domain = "gameplay.") { if (TrySourceKey(source,out var key,domain)) Bind(target, key); else if (target != null) { ClearBinding(target); target.text = source; } }
    public static void BindSource(UnityEngine.UI.Text target) { if (target != null) BindSource(target, target.text); }
    public static void BindSource(UnityEngine.UI.Text target, string source,string domain = "gameplay.") { if (TrySourceKey(source,out var key,domain)) Bind(target, key); else if (target != null) { ClearBinding(target); target.text = source; } }

    internal static void NotifyInitialized() => LanguageChanged?.Invoke();

    private static void ClearBinding(Component target)
    {
        var binding = target.GetComponent<LocalizedGameText>();
        if (binding != null) binding.Clear();
    }
}
