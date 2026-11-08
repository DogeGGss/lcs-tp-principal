using System;
using System.IO;
using System.Collections;
using NUnit.Framework;
using Riftwalker.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

public sealed class LocalizationTests
{
    private string temporary;
    [SetUp] public void Setup() { temporary = Path.Combine(Path.GetTempPath(),"Riftwalker-L10n-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary); }
    [TearDown] public void Cleanup() { if (Directory.Exists(temporary)) Directory.Delete(temporary,true); }

    [Test] public void CatalogFallsBackToSpanishAndMarksUnknownKeys()
    {
        var catalog = new TranslationCatalog();
        catalog.Add("es",new[]{new TranslationEntry{key="play",text="Jugar"}});
        catalog.Add("en",new[]{new TranslationEntry{key="other",text="Other"}});
        Assert.AreEqual("Jugar",catalog.GetText("pt","play"));
        Assert.AreEqual("Jugar",catalog.GetText("en","play"));
        Assert.AreEqual("[unknown]",catalog.GetText("en","unknown"));
    }
    [Test] public void FormattingPreservesPlayerNamesAndUsesSelectedCulture()
    {
        var catalog = new TranslationCatalog();
        catalog.Add("es",new[]{new TranslationEntry{key="score",text="{0}: {1:F1}"}});
        catalog.Add("en",new[]{new TranslationEntry{key="score",text="{0}: {1:F1}"}});
        Assert.AreEqual("Jugar: 1.5",catalog.Format("en","score","Jugar",1.5));
        StringAssert.Contains("1,5",catalog.Format("es","score","Jugar",1.5));
    }
    [Test] public void CatalogRejectsMissingValuesAndDuplicateKeys()
    {
        var catalog = new TranslationCatalog();
        Assert.Throws<ArgumentException>(()=>catalog.Add("en",new[]{new TranslationEntry{key="empty",text=""}}));
        catalog.Add("en",new[]{new TranslationEntry{key="play",text="Play"}});
        Assert.Throws<ArgumentException>(()=>catalog.Add("en",new[]{new TranslationEntry{key="play",text="Again"}}));
    }
    [Test] public void LanguagePreferenceIsIndependentOfPlayerDataAndSurvivesRestart()
    {
        var file=Path.Combine(temporary,LanguagePreference.FileName);
        var player=Path.Combine(temporary,"jugador.json");File.WriteAllText(player,"{\"name\":\"Soledad\"}");
        Assert.AreEqual("es",LanguagePreference.Load(file));
        LanguagePreference.Save(file,"en");
        Assert.AreEqual("en",LanguagePreference.Load(file));
        LanguagePreference.Save(file,"es");
        Assert.AreEqual("es",LanguagePreference.Load(file));
        Assert.AreEqual("{\"name\":\"Soledad\"}",File.ReadAllText(player));
        File.WriteAllText(file,"broken");Assert.AreEqual("es",LanguagePreference.Load(file));
        Assert.Throws<ArgumentException>(()=>LanguagePreference.Save(file,"../en"));
    }
    [UnityTest] public IEnumerator BindingRefreshesWithUnityLocaleAndCanBeCleared()
    {
        yield return LocalizationSettings.InitializationOperation;
        var old=LocalizationSettings.SelectedLocale;
        var go=new GameObject("Localized test",typeof(RectTransform),typeof(UnityEngine.UI.Text));
        try
        {
            var text=go.GetComponent<UnityEngine.UI.Text>();
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("es");
            L10n.Bind(text,"ui.name.short",2);
            yield return null;
            Assert.AreEqual("Faltan 2 caracteres: el mínimo es 3.",text.text);
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");
            float until=Time.realtimeSinceStartup+10f;
            while(text.text!="2 more characters required: the minimum is 3."&&Time.realtimeSinceStartup<until)yield return null;
            Assert.AreEqual("2 more characters required: the minimum is 3.",text.text);
            L10n.BindSource(text,"Player nickname is untouched");
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("es");
            yield return null;
            Assert.AreEqual("Player nickname is untouched",text.text);
        }
        finally { LocalizationSettings.SelectedLocale=old; UnityEngine.Object.DestroyImmediate(go); }
    }
}