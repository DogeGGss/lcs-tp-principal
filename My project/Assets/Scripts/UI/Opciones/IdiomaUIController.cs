using System;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Immediate, shared language preference; other options keep their existing Apply flow.</summary>
public sealed class IdiomaUIController : MonoBehaviour
{
    private OpcionesKit.Segmentos choices;
    private string[] codes;
    private void OnEnable() { L10n.LanguageChanged += Refresh; if (choices != null) Refresh(); }
    private void OnDisable() { L10n.LanguageChanged -= Refresh; }
    private void Start()
    {
        var kit = OpcionesKit.Armar((RectTransform)transform,"Idioma");
        var languages = L10n.Languages;
        codes = languages.Select(l => l.code).ToArray();
        var names = languages.Select(l => l.name).ToArray();
        float y = 126f;
        kit.Grupo("Idioma del juego",ref y);
        kit.Ayuda("El cambio se aplica al instante y se comparte con el launcher.",ref y);
        choices = kit.FilaSegmentos("Idioma",ref y,names,Select);
        Refresh();
    }
    private void Select(int index)
    {
        if (index < 0 || index >= codes.Length) return;
        try { L10n.SetLanguage(codes[index]); Refresh(); }
        catch (System.IO.IOException e) { Debug.LogWarning("Unable to save language preference: " + e.Message); }
        catch (UnauthorizedAccessException e) { Debug.LogWarning("Unable to save language preference: " + e.Message); }
    }
    private void Refresh()
    {
        if (choices == null || codes == null) return;
        choices.Marcar(Mathf.Max(0,Array.IndexOf(codes,L10n.LanguageCode)));
    }
}