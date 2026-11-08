using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Riftwalker.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>The JSON catalogs are the source of truth for both executables.</summary>
public sealed class LocalizationCatalogBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) { Sync(); AddressableAssetSettings.BuildPlayerContent(); }

    [MenuItem("Riftwalker/Idiomas/Sincronizar catálogos")]
    public static void Sync()
    {
        const string root = "Assets/Localization";
        Directory.CreateDirectory(root + "/Locales");
        Directory.CreateDirectory(root + "/Tables");
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<LocaleManifest>(File.ReadAllText("Assets/Resources/Localization/locales.json"));
        if (manifest?.locales == null || manifest.locales.Length == 0) throw new BuildFailedException("No localization locales defined.");
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in manifest.locales)
        {
            var entries = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles("Assets/Resources/Localization/" + definition.code, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var file = JsonUtility.FromJson<TranslationFile>(File.ReadAllText(path));
                if (file?.entries == null) throw new BuildFailedException("Invalid translation file: " + path);
                foreach (var entry in file.entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.text)) throw new BuildFailedException("Empty translation: " + path + " / " + entry.key);
                    if (entries.ContainsKey(entry.key)) throw new BuildFailedException("Duplicate translation: " + definition.code + " / " + entry.key);
                    entries.Add(entry.key, entry.text);
                }
            }
            if (entries.Count == 0) throw new BuildFailedException("Empty locale: " + definition.code);
            data.Add(definition.code, entries);
        }
        if (!data.TryGetValue("es",out var source)) throw new BuildFailedException("Spanish source locale is missing.");
        foreach (var locale in data)
            if (!new HashSet<string>(source.Keys).SetEquals(locale.Value.Keys)) throw new BuildFailedException("Translation keys differ: " + locale.Key);

        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(root + "/LocalizationSettings.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings,root + "/LocalizationSettings.asset");
        }
        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        var locales = new List<Locale>();
        foreach (var definition in manifest.locales)
        {
            var path = root + "/Locales/" + definition.code + ".asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null) { locale = Locale.CreateLocale(definition.code); AssetDatabase.CreateAsset(locale,path); }
            locale.LocaleName = definition.name;
            LocalizationEditorSettings.AddLocale(locale);
            EditorUtility.SetDirty(locale);
            locales.Add(locale);
        }
        settings.GetStartupLocaleSelectors().Clear();
        settings.GetStartupLocaleSelectors().Add(new SavedLanguageSelector());
        settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = "es" });
        EditorUtility.SetDirty(settings);
        var collection = LocalizationEditorSettings.GetStringTableCollection(L10n.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(L10n.TableName,root + "/Tables",locales);
        foreach (var locale in locales)
        {
            var table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null) table = collection.AddNewTable(locale.Identifier) as StringTable;
            foreach (var entry in data[locale.Identifier.Code]) table.AddEntry(entry.Key,entry.Value);
            LocalizationEditorSettings.SetPreloadTableFlag(table,true);
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(typeof(LocalizationCatalogBuild),collection);
        AssetDatabase.SaveAssets();
        Debug.Log("Localization catalogs synchronized: " + source.Count + " keys × " + locales.Count + " locales.");
    }

    public static void SyncAndBuild()
    {
        Sync();
        AddressableAssetSettings.BuildPlayerContent();
    }
}