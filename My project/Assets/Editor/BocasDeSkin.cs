using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// F35 (Skins de armas), US 381: la boca de cada arma de primera persona para el fogonazo y la estela de las legendarias:
// un objeto vacío (BocaSkin) en cada arma del prefab del jugador, que se mueve a mano (EfectosDeSkin lo busca).
// - Riftwalker > Armas > Crear bocas de skin: se lo agrega a las armas que no lo tienen, en la punta del modelo.
// - Skins de prueba > Guardar en el prefab: deja en el prefab la posición a la que se lo movió con el juego andando.
public static class BocasDeSkin
{
    private const string Jugador = "Assets/Prefabs/Player.prefab";

    [MenuItem("Riftwalker/Armas/Crear bocas de skin")]
    private static void Crear()
    {
        if (Application.isPlaying) { Debug.LogWarning("Crear bocas de skin: pará el juego primero."); return; }
        GameObject raiz = PrefabUtility.LoadPrefabContents(Jugador);
        var creadas = new List<string>();
        try
        {
            WeaponSwitcher armas = raiz.GetComponentInChildren<WeaponSwitcher>(true);
            if (armas == null) { Debug.LogWarning("Crear bocas de skin: el prefab del jugador no tiene WeaponSwitcher."); return; }
            foreach (GameObject arma in Armas(armas))
            {
                if (EfectosDeSkin.BocaDe(arma) != null) continue;
                bool activa = arma.activeSelf; // apagada, sus piezas no tienen tamaño
                arma.SetActive(true);
                Crear(arma);
                arma.SetActive(activa);
                creadas.Add(arma.name);
            }
            if (creadas.Count > 0) PrefabUtility.SaveAsPrefabAsset(raiz, Jugador);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        Debug.Log(creadas.Count > 0 ? "Bocas de skin creadas en: " + string.Join(", ", creadas)
            : "Todas las armas ya tenían su " + EfectosDeSkin.NombreBoca + ".");
    }

    private static IEnumerable<GameObject> Armas(WeaponSwitcher armas)
    {
        foreach (GameObject a in armas.Principales()) yield return a;
        foreach (GameObject a in armas.Secundarias()) yield return a;
    }

    // En la punta del modelo (lo más lejos hacia donde mira la cámara), colgado de la pieza más grande del arma (así
    // acompaña al modelo cuando se mueve), mirando hacia adelante.
    private static void Crear(GameObject arma)
    {
        Camera cam = arma.GetComponentInParent<Camera>(true);
        Vector3 adelante = cam != null ? cam.transform.forward : arma.transform.forward;
        Vector3 arriba = cam != null ? cam.transform.up : Vector3.up;
        Bounds b = default;
        bool hay = false;
        Renderer mayor = null;
        float volumen = -1f;
        foreach (Renderer r in arma.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            Vector3 s = r.bounds.size;
            if (s.x * s.y * s.z > volumen) { volumen = s.x * s.y * s.z; mayor = r; }
        }
        Vector3 punta = arma.transform.position;
        if (hay)
        {
            float hasta = Mathf.Abs(adelante.x) * b.extents.x + Mathf.Abs(adelante.y) * b.extents.y + Mathf.Abs(adelante.z) * b.extents.z;
            punta = b.center + adelante * hasta;
        }
        var boca = new GameObject(EfectosDeSkin.NombreBoca) { layer = arma.layer };
        boca.transform.SetParent(mayor != null ? mayor.transform : arma.transform, false);
        boca.transform.SetPositionAndRotation(punta, Quaternion.LookRotation(adelante, arriba));
    }

    /// <summary>Copia al prefab del jugador la posición y el giro (respecto de su padre) de la boca de esa arma.</summary>
    public static void Guardar(string nombreDelArma, Transform boca)
    {
        if (boca == null) return;
        GameObject raiz = PrefabUtility.LoadPrefabContents(Jugador);
        bool guardada = false;
        try
        {
            WeaponSwitcher armas = raiz.GetComponentInChildren<WeaponSwitcher>(true);
            if (armas != null)
                foreach (GameObject arma in Armas(armas))
                {
                    if (arma.name != nombreDelArma) continue;
                    Transform enElPrefab = EfectosDeSkin.BocaDe(arma);
                    if (enElPrefab == null) break;
                    enElPrefab.localPosition = boca.localPosition;
                    enElPrefab.localRotation = boca.localRotation;
                    PrefabUtility.SaveAsPrefabAsset(raiz, Jugador);
                    guardada = true;
                    break;
                }
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        if (guardada) Debug.Log("Boca de " + nombreDelArma + " guardada en el prefab del jugador.");
        else Debug.LogWarning("No encontré la boca de " + nombreDelArma + " en el prefab del jugador (" + Jugador + ").");
    }
}
