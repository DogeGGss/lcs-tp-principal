using System;
using System.Collections;
using Riftwalker.Releases;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// F22 / US 204. No token is shipped to players. Failure to reach GitHub does not block Photon.
// In the editor and in development builds nothing is blocked: the team tests versions that are not published yet
// (Photon still only joins players with the same version).
public static class VersionDelJuego
{
    private const string Repositorio = "https://github.com/DogeGGss/lcs-tp-principal";
    private const string Api = "https://api.github.com/repos/DogeGGss/lcs-tp-principal/releases?per_page=100&page=";
    // The latest stable version comes from its release.json: a plain download, not GitHub's API, so it does not count
    // against the API's 60 requests per hour per IP (a whole network, like the university's, shares that limit).
    private const string UltimaEstable = Repositorio + "/releases/latest/download/release.json";
    private static bool buscando;
    private static float comprobadaEn = -1000;
    private static string versionNueva;
    [Serializable] private class Manifiesto { public string version; }
    [Serializable] private class Lista { public Release[] releases; }
    [Serializable] private class Release { public string tag_name; public bool draft, prerelease; public Asset[] assets; }
    [Serializable] private class Asset { public string name; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() { buscando = false; comprobadaEn = -1000; versionNueva = null; }

    public static IEnumerator Comprobar(Action<string> terminado)
    {
        if (Application.isEditor || Debug.isDebugBuild) { terminado(null); yield break; }
        while (buscando) yield return null;
        if (Time.realtimeSinceStartup - comprobadaEn < 60) { terminado(Aviso()); yield break; }
        ReleaseVersion local;
        if (!ReleaseVersion.TryParse(Application.version, out local)) { terminado(null); yield break; }
        buscando = true;
        ReleaseVersion ultima = null;
        float limite = Time.realtimeSinceStartup + 10;
        try
        {
            // An experimental version also compares against the newer experimentals, which only the API lists.
            if (local.IsExperimental) yield return BuscarConExperimentales(limite, v => ultima = v);
            else yield return BuscarEstable(limite, v => ultima = v);
            versionNueva = ultima != null && ultima.CompareTo(local) > 0 ? ultima.ToString() : null;
            comprobadaEn = Time.realtimeSinceStartup;
        }
        finally { buscando = false; }
        terminado(Aviso());
    }

    // Without an answer (or with 404: the latest stable release does not have the launcher format yet) it reports nothing.
    private static IEnumerator BuscarEstable(float limite, Action<ReleaseVersion> encontrada)
    {
        using (var request = UnityWebRequest.Get(UltimaEstable))
        {
            request.timeout = Math.Max(1, Mathf.CeilToInt(limite - Time.realtimeSinceStartup));
            var sent = request.SendWebRequest();
            while (!sent.isDone && Time.realtimeSinceStartup < limite) yield return null;
            if (!sent.isDone) { request.Abort(); yield break; }
            if (request.result != UnityWebRequest.Result.Success) yield break;
            Manifiesto manifiesto = null;
            try { manifiesto = JsonUtility.FromJson<Manifiesto>(request.downloadHandler.text.TrimStart('﻿')); }
            catch (ArgumentException) { }
            ReleaseVersion v;
            if (manifiesto != null && ReleaseVersion.TryParse(manifiesto.version, out v) && !v.IsExperimental) encontrada(v);
        }
    }

    private static IEnumerator BuscarConExperimentales(float limite, Action<ReleaseVersion> encontrada)
    {
        ReleaseVersion ultima = null;
        for (int page = 1; ; page++)
        {
            using (var request = UnityWebRequest.Get(Api + page))
            {
                request.timeout = Math.Max(1, Mathf.CeilToInt(limite - Time.realtimeSinceStartup));
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                var sent = request.SendWebRequest();
                while (!sent.isDone && Time.realtimeSinceStartup < limite) yield return null;
                if (!sent.isDone) { request.Abort(); yield break; }
                if (request.result != UnityWebRequest.Result.Success) yield break;
                Lista lista = null;
                try { lista = JsonUtility.FromJson<Lista>("{\"releases\":" + request.downloadHandler.text + "}"); }
                catch (ArgumentException) { }
                if (lista == null || lista.releases == null) yield break;
                foreach (var release in lista.releases)
                {
                    ReleaseVersion v;
                    if (release.draft || !ReleaseVersion.TryParse(release.tag_name, out v) || release.prerelease != v.IsExperimental
                        || release.assets == null) continue;
                    bool manifest = false, zip = false;
                    foreach (var asset in release.assets)
                    { manifest |= asset.name == "release.json"; zip |= asset.name == "Project-Riftwalker-v" + v + ".zip"; }
                    if (manifest && zip && (ultima == null || v.CompareTo(ultima) > 0)) ultima = v;
                }
                if (lista.releases.Length < 100) break;
                if (Time.realtimeSinceStartup >= limite) yield break;
            }
        }
        if (ultima != null) encontrada(ultima);
    }
    private static string Aviso() => versionNueva == null ? null :
        "Hay una versión nueva (" + versionNueva + "). Actualizá el juego desde el launcher para jugar online.";

    public static void MostrarEnMenu(Transform panel)
    {
        if (panel == null || panel.Find("Version del juego") != null) return;
        var existing = panel.GetComponentInChildren<TMP_Text>(true);
        var go = new GameObject("Version del juego", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(panel, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-28, 18); rect.sizeDelta = new Vector2(300, 32);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (existing != null) text.font = existing.font;
        text.text = "v" + Application.version; text.fontSize = 20;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.color = new Color32(142, 150, 163, 255); text.raycastTarget = false;
    }
}
