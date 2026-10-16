using UnityEngine;

[CreateAssetMenu(menuName = "Riftwalker/Logros online", fileName = "LogrosOnline")]
public sealed class LogrosOnlineConfig : ScriptableObject
{
    public bool habilitado;
    [Tooltip("Project URL, por ejemplo https://xxxx.supabase.co")]
    public string url;
    [Tooltip("Solo publishable key (sb_publishable_...). Nunca service_role ni secret key.")]
    public string clavePublica;
    public bool Valido => habilitado && System.Uri.TryCreate(url, System.UriKind.Absolute, out var u)
        && u.Scheme == "https" && !string.IsNullOrWhiteSpace(clavePublica)
        && clavePublica.Trim().StartsWith("sb_publishable_");
}
