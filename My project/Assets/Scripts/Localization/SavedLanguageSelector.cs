using System;
using Riftwalker.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[Serializable]
public sealed class SavedLanguageSelector : IStartupLocaleSelector
{
    public Locale GetStartupLocale(ILocalesProvider availableLocales) =>
        availableLocales.GetLocale(LanguagePreference.Load(L10n.PreferencePath));
}