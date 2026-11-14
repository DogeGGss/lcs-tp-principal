using UnityEditor;
using UnityEngine;

// F35 (Skins de armas), US 378, para probar en el editor mientras no está el apartado del perfil (US 379): el catálogo de
// skins, por arma, con su rareza y su estado (bloqueada, disponible o equipada). Se pueden desbloquear, equipar y, con el
// juego andando, ponérsela al arma que se tiene en la mano para ver los colores. Abajo, el ajuste de la boca del arma
// para el fogonazo y la estela de las legendarias (US 381). No llega a la versión del juego.
public class SkinsDePrueba : EditorWindow
{
    private Vector2 scroll;
    private string abierta;

    [MenuItem("Riftwalker/Armas/Skins de prueba...")]
    private static void Abrir() => GetWindow<SkinsDePrueba>("Skins de prueba").Show();

    private void OnEnable() => SkinsDeArmas.Cambio += Repaint;
    private void OnDisable() => SkinsDeArmas.Cambio -= Repaint;
    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        GameObject enLaMano = EnLaMano();
        EditorGUILayout.HelpBox(enLaMano != null ? "En la mano: " + enLaMano.name
            : "Dale Play y sacá un arma para ver las skins puestas.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Desbloquear todas"))
            {
                SkinsDeArmas.SinNube = false;
                foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins) SkinsDeArmas.Desbloquear(s.id);
            }
            if (GUILayout.Button("Bloquear todas"))
            {
                SkinsDeArmas.SinNube = true; // si no, las de la nube vuelven en la próxima sincronización
                SkinsDeArmas.Quitar();
            }
            if (enLaMano != null && GUILayout.Button("Sacar la skin")) SkinDeArma.Poner(enLaMano, null);
        }
        if (SkinsDeArmas.SinNube)
            EditorGUILayout.HelpBox("Bloqueadas para probar: no se traen las skins de la nube hasta apretar Desbloquear todas.", MessageType.Info);
        Boca(enLaMano);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
        {
            CatalogoDeSkins.Skin equipada = SkinsDeArmas.EquipadaDe(a.id);
            bool ver = EditorGUILayout.Foldout(abierta == a.id, a.nombre + (equipada != null ? "  · " + equipada.Estilo : ""), true);
            if (ver != (abierta == a.id)) abierta = ver ? a.id : null;
            if (!ver) continue;
            foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.DelArma(a.id)) Fila(s, enLaMano);
        }
        EditorGUILayout.EndScrollView();
    }

    private static void Fila(CatalogoDeSkins.Skin s, GameObject enLaMano)
    {
        SkinsDeArmas.Estado estado = SkinsDeArmas.EstadoDe(s);
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (Color c in CatalogoDeSkins.ColoresDeBarra(s)) Muestra(c);
            var estilo = new GUIStyle(EditorStyles.label) { normal = { textColor = PerfilUI.ColorRareza(s.rareza) } };
            GUILayout.Label(s.nombre + "  (" + PerfilUI.NombreRareza(s.rareza) + ")", estilo, GUILayout.Width(230));
            GUILayout.Label(estado.ToString(), GUILayout.Width(80));
            if (estado == SkinsDeArmas.Estado.Bloqueada)
            {
                if (GUILayout.Button("Desbloquear", GUILayout.Width(95))) SkinsDeArmas.Desbloquear(s.id);
            }
            else if (estado == SkinsDeArmas.Estado.Disponible)
            {
                if (GUILayout.Button("Equipar", GUILayout.Width(95))) SkinsDeArmas.Equipar(s);
            }
            else if (GUILayout.Button("Desequipar", GUILayout.Width(95))) SkinsDeArmas.Desequipar(s.arma.id);
            using (new EditorGUI.DisabledScope(enLaMano == null))
                if (GUILayout.Button("Ponérsela", GUILayout.Width(80))) SkinDeArma.Poner(enLaMano, s);
        }
    }

    // US 381: la boca del arma en la mano (de dónde salen el fogonazo y la estela de las legendarias en primera persona).
    // Es el vacío BocaSkin del arma: se lo selecciona, se lo mueve (y se lo gira, para la dirección de la llama) en la
    // escena con el juego andando, "Probar" tira el fogonazo y una estela del Dragón Abisal sin disparar, y "Guardar en
    // el prefab" deja esa posición en el prefab del jugador (lo que se mueve con el juego andando se pierde al pararlo).
    // Un arma sin el vacío usa la punta del modelo con un ajuste a ojo (los deslizadores).
    private static void Boca(GameObject enLaMano)
    {
        if (enLaMano == null) return;
        ShopItem ficha = SkinsEnLasArmas.FichaDeArma(enLaMano);
        CatalogoDeSkins.Arma arma = ficha != null ? CatalogoDeSkins.ArmaDeFicha(ficha.name) : null;
        if (arma == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Boca de " + arma.nombre + " (fogonazo y estela de las legendarias)", EditorStyles.boldLabel);
        Transform boca = EfectosDeSkin.BocaDe(enLaMano);
        if (boca != null)
        {
            EditorGUILayout.HelpBox("Sale del vacío " + EfectosDeSkin.NombreBoca + ": seleccionalo, movelo en la escena " +
                "(la flecha azul, hacia donde sale la llama) y probá. Al final, guardalo en el prefab.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seleccionar la boca")) { Selection.activeTransform = boca; EditorGUIUtility.PingObject(boca); }
                if (GUILayout.Button("Probar"))
                    EfectosDeSkin.Prueba(enLaMano, ficha, EfectosDeSkin.De(CatalogoDeSkins.De(arma.id + "-dragon-abisal")));
                if (GUILayout.Button("Guardar en el prefab")) BocasDeSkin.Guardar(enLaMano.name, boca);
            }
            return;
        }
        EditorGUILayout.HelpBox("Esta arma no tiene el vacío " + EfectosDeSkin.NombreBoca + ": Riftwalker > Armas > " +
            "Crear bocas de skin (con el juego parado).", MessageType.Info);
        ModelosDeArmas modelos = ModelosDeArmas.Actual;
        Vector2 antes = modelos.CorrimientoDeBoca(arma.id);
        Vector2 ahora = antes;
        ahora.x = EditorGUILayout.Slider("Derecha / izquierda", antes.x, -0.3f, 0.3f);
        ahora.y = EditorGUILayout.Slider("Arriba / abajo", antes.y, -0.3f, 0.3f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Probar"))
                EfectosDeSkin.Prueba(enLaMano, ficha, EfectosDeSkin.De(CatalogoDeSkins.De(arma.id + "-dragon-abisal")));
            if (GUILayout.Button("Volver a 0")) ahora = Vector2.zero;
        }
        if (ahora != antes)
        {
            modelos.CorrerBoca(arma.id, ahora);
            EditorUtility.SetDirty(modelos);
            AssetDatabase.SaveAssetIfDirty(modelos);
        }
    }

    private static void Muestra(Color c)
    {
        Rect r = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
        EditorGUI.DrawRect(r, c);
    }

    // El arma de fuego en la mano o, si no, el cuchillo.
    private static GameObject EnLaMano()
    {
        if (!Application.isPlaying) return null;
        WeaponSwitcher armas = Object.FindFirstObjectByType<WeaponSwitcher>();
        if (armas == null) return null;
        GameObject arma = armas.HeldPrimary ?? armas.HeldSecondary;
        if (arma == null && armas.meleeScript != null && armas.meleeScript.CurrentViewModel != null &&
            armas.meleeScript.CurrentViewModel.activeInHierarchy) arma = armas.meleeScript.CurrentViewModel;
        return arma;
    }
}
