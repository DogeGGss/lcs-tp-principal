using System.IO;
using UnityEditor;
using UnityEngine;

// Manos de las armas en primera persona (US 173). En Play, con "Ajustar a mano" (BrazosEnCamara), cada arma y sus
// agarres se ubican a mano; BrazosEnCamara guarda dónde quedaron en Library/AjusteDeManos.json. Acá se pasa eso al
// Player.prefab (y el cuchillo, a su propio prefab) al salir del Play, o al abrir Unity si se cerró en medio del ajuste.
[InitializeOnLoad]
public static class AjusteDeManos
{
    private const string Prefab = "Assets/Prefabs/Player.prefab";
    private const string OpcionAjustar = "Riftwalker/Manos de las armas/Ajustar a mano en Play";

    static AjusteDeManos()
    {
        EditorApplication.playModeStateChanged += cambio =>
        {
            if (cambio == PlayModeStateChange.EnteredEditMode) Aplicar(false);
        };
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Aplicar(false);
        };
    }

    [MenuItem(OpcionAjustar)]
    private static void Alternar()
    {
        EditorPrefs.SetBool(BrazosEnCamara.PreferenciaAjuste, !EditorPrefs.GetBool(BrazosEnCamara.PreferenciaAjuste, false));
    }

    [MenuItem(OpcionAjustar, true)]
    private static bool MarcarAlternar()
    {
        Menu.SetChecked(OpcionAjustar, EditorPrefs.GetBool(BrazosEnCamara.PreferenciaAjuste, false));
        return true;
    }

    [MenuItem("Riftwalker/Manos de las armas/Guardar en el prefab el último ajuste")]
    private static void AplicarDeNuevo()
    {
        Aplicar(true);
    }

    private static void Aplicar(bool aunqueYaEste)
    {
        if (!File.Exists(BrazosEnCamara.ArchivoAjuste)) return;
        BrazosEnCamara.Ajuste ajuste = JsonUtility.FromJson<BrazosEnCamara.Ajuste>(File.ReadAllText(BrazosEnCamara.ArchivoAjuste));
        if (ajuste == null || ajuste.armas.Count == 0 || (ajuste.aplicado && !aunqueYaEste)) return;

        int cambiadas = 0;
        string rutaCuchillo = null;
        BrazosEnCamara.Agarres cuchillo = null;

        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            WeaponSwitcher camara = raiz.GetComponentInChildren<WeaponSwitcher>(true);
            GameObject vistaCuchillo = PrefabDelCuchillo(raiz);
            if (vistaCuchillo != null) rutaCuchillo = AssetDatabase.GetAssetPath(vistaCuchillo);
            foreach (BrazosEnCamara.Agarres a in ajuste.armas)
            {
                // El cuchillo se crea al empezar el juego desde su propio prefab ("Knife_ViewModel(Clone)").
                if (vistaCuchillo != null && a.arma == vistaCuchillo.name + "(Clone)")
                {
                    cuchillo = a;
                    continue;
                }
                Transform arma = camara.transform.Find(a.arma);
                if (arma != null && PonerArma(arma, a)) cambiadas++;
            }
            if (cambiadas > 0) PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }

        if (cuchillo != null && !string.IsNullOrEmpty(rutaCuchillo))
        {
            GameObject vista = PrefabUtility.LoadPrefabContents(rutaCuchillo);
            try
            {
                if (PonerArma(vista.transform, cuchillo))
                {
                    PrefabUtility.SaveAsPrefabAsset(vista, rutaCuchillo);
                    cambiadas++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(vista);
            }
        }

        ajuste.aplicado = true;
        File.WriteAllText(BrazosEnCamara.ArchivoAjuste, JsonUtility.ToJson(ajuste, true));
        Debug.Log(cambiadas > 0
            ? $"Manos de las armas: se guardó en los prefabs lo que se ajustó en Play ({cambiadas} armas)."
            : "Manos de las armas: el ajuste ya estaba en los prefabs.");
    }

    // El prefab del cuchillo en primera persona, el que el jugador crea al empezar (MeleeWeaponHolder).
    private static GameObject PrefabDelCuchillo(GameObject jugador)
    {
        MeleeWeaponHolder cuchillo = jugador.GetComponentInChildren<MeleeWeaponHolder>(true);
        if (cuchillo == null) return null;
        var datos = new SerializedObject(cuchillo).FindProperty("defaultWeapon").objectReferenceValue as MeleeWeaponData;
        return datos != null ? datos.viewModelPrefab : null;
    }

    private static bool PonerArma(Transform arma, BrazosEnCamara.Agarres a)
    {
        Transform derecho = arma.Find(BrazosEnCamara.AgarreDerecho), izquierdo = arma.Find(BrazosEnCamara.AgarreIzquierdo);
        if (derecho == null || izquierdo == null) return false;
        bool cambio = Poner(arma, a.lugar, a.giro);
        if (a.escala != Vector3.zero && arma.localScale != a.escala) // los ajustes viejos no tienen el tamaño
        {
            arma.localScale = a.escala;
            cambio = true;
        }
        cambio |= Poner(derecho, a.derecho, a.derechoGiro);
        cambio |= Poner(izquierdo, a.izquierdo, a.izquierdoGiro);
        return cambio;
    }

    // Solo toca lo que cambió, así las armas que no se movieron no ensucian el prefab.
    private static bool Poner(Transform t, Vector3 lugar, Quaternion giro)
    {
        bool cambio = false;
        if (t.localPosition != lugar) { t.localPosition = lugar; cambio = true; }
        if (t.localRotation != giro) { t.localRotation = giro; cambio = true; }
        return cambio;
    }
}
