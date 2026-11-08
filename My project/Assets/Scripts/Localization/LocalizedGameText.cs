using System;
using Riftwalker.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

/// <summary>A key and its arguments are retained even after the visible language changes.</summary>
[DisallowMultipleComponent]
public sealed class LocalizedGameText : MonoBehaviour
{
    private TMP_Text tmp;
    private UnityEngine.UI.Text legacy;
    private LocalizeStringEvent localized;
    private string key;
    private object[] arguments;

    public void Configure(TMP_Text target, string entry, params object[] args) { tmp = target; Configure(entry, args); }
    public void Configure(UnityEngine.UI.Text target, string entry, params object[] args) { legacy = target; Configure(entry, args); }
    private void Configure(string entry, object[] args)
    {
        key = entry; arguments = args; lastSet = null;
        enabled = true;
        // Also subscribed here: OnEnable doesn't run in edit mode (tests, editor tools).
        L10n.LanguageChanged -= Refresh; L10n.LanguageChanged += Refresh;
        if (localized == null)
        {
            localized = GetComponent<LocalizeStringEvent>() ?? gameObject.AddComponent<LocalizeStringEvent>();
            localized.OnUpdateString.AddListener(Apply);
        }
        localized.StringReference.SetReference(L10n.TableName, key);
        localized.enabled = true;
        Refresh();
    }
    public void Clear()
    {
        key = null; arguments = null; lastSet = null;
        if (localized != null) localized.enabled = false;
        enabled = false;
    }
    private void OnEnable() { L10n.LanguageChanged += Refresh; Refresh(); }
    private void OnDisable() { L10n.LanguageChanged -= Refresh; }
    private void OnDestroy() { L10n.LanguageChanged -= Refresh; if (localized != null) localized.OnUpdateString.RemoveListener(Apply); }
    private void Refresh()
    {
        // A text destroyed without ever being active never gets OnDestroy: it lets go of the event here.
        if (this == null) { L10n.LanguageChanged -= Refresh; return; }
        if (!string.IsNullOrEmpty(key) && !ReplacedFromOutside()) SetText(L10n.T(key, arguments));
    }
    private void Apply(string value) { if (!string.IsNullOrEmpty(key) && !ReplacedFromOutside()) SetText(TranslationCatalog.FormatText(L10n.LanguageCode, value, arguments)); }

    // If other code wrote a different text into the label, that text wins: the binding lets go instead of
    // bringing the old key back on the next language change.
    private string lastSet;
    private bool ReplacedFromOutside()
    {
        string current = tmp != null ? tmp.text : legacy != null ? legacy.text : null;
        if (lastSet == null || current == lastSet) return false;
        Clear();
        return true;
    }
    private void SetText(string value)
    {
        lastSet = value;
        if (tmp != null) tmp.text = value;
        if (legacy != null) legacy.text = value;
    }
}
