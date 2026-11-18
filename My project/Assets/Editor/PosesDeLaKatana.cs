using UnityEditor;
using UnityEngine;

// US 395: para ajustar en Play cómo se mueve la katana (MovimientoDeKatana). Se elige una pose, la katana se queda
// quieta en ella y se cambian los números viendo el resultado; con los botones de Probar se ve el ataque entero (lento,
// si se quiere). "Guardar en el prefab" las pasa a Resources/Katana/Katana_ViewModel; si no, se pierden al salir del Play.
public class PosesDeLaKatana : EditorWindow
{
    private int pose = 1;
    private bool fija = true;
    private Vector2 scroll;

    [MenuItem("Riftwalker/Armas/Poses de la katana")]
    public static void Abrir() => GetWindow<PosesDeLaKatana>("Poses de la katana").Show();

    private void OnEnable() => EditorApplication.playModeStateChanged += AlCambiarPlay;
    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= AlCambiarPlay;
        MovimientoDeKatana.PoseFija = -1;
        MovimientoDeKatana.Probando = -1;
    }

    private void AlCambiarPlay(PlayModeStateChange cambio)
    {
        MovimientoDeKatana.PoseFija = -1;
        MovimientoDeKatana.Probando = -1;
    }

    private static MovimientoDeKatana EnLaMano()
    {
        if (!Application.isPlaying) return null;
        foreach (MovimientoDeKatana m in Object.FindObjectsByType<MovimientoDeKatana>(FindObjectsSortMode.None))
            if (m.isActiveAndEnabled && m.GetComponentInParent<MeleeAttack>() != null) return m;
        return null;
    }

    private void OnGUI()
    {
        MovimientoDeKatana mov = EnLaMano();
        if (mov == null)
        {
            EditorGUILayout.HelpBox("Entrá en Play, date la katana (Riftwalker > Armas > Darme un arma...) y sacala con el 3.", MessageType.Info);
            MovimientoDeKatana.PoseFija = -1;
            return;
        }
        if (BrazosEnCamara.AjustandoAMano)
            EditorGUILayout.HelpBox("Con «Ajustar a mano en Play» prendido la katana no se mueve: apagalo para ver las poses.", MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Pose", EditorStyles.boldLabel);
        pose = GUILayout.SelectionGrid(pose, MovimientoDeKatana.Nombres, 2);
        fija = EditorGUILayout.ToggleLeft("Dejar la katana quieta en esta pose", fija);
        MovimientoDeKatana.PoseFija = fija ? pose : -1;

        MovimientoDeKatana.Pose p = mov.Poses()[pose];
        EditorGUILayout.Space();
        if (pose == 0)
            EditorGUILayout.HelpBox("La quieta es como quedó en el ajuste a mano (Riftwalker > Manos de las armas).", MessageType.None);
        else
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Brazo y katana juntos (metros: x derecha, y arriba, z adelante)", EditorStyles.miniBoldLabel);
            Vector3 brazo = Deslizadores(p.brazo, 0.4f, 0.005f);
            EditorGUILayout.LabelField("Muñeca: giro alrededor de la mano (grados: x baja la punta, y a los costados, z inclina)", EditorStyles.miniBoldLabel);
            Vector3 muneca = Deslizadores(Normalizar(p.muneca), 180f, 1f);
            EditorGUILayout.LabelField("La katana sola, sin el brazo (metros)", EditorStyles.miniBoldLabel);
            Vector3 corrimiento = Deslizadores(p.corrimiento, 0.2f, 0.005f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mov, "Pose de la katana");
                p.brazo = brazo; p.muneca = muneca; p.corrimiento = corrimiento;
                EditorUtility.SetDirty(mov);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copiar de la anterior") && pose > 1)
                {
                    Undo.RecordObject(mov, "Pose de la katana");
                    MovimientoDeKatana.Pose a = mov.Poses()[pose - 1];
                    p.brazo = a.brazo; p.muneca = a.muneca; p.corrimiento = a.corrimiento;
                }
                if (GUILayout.Button("Volver a cero"))
                {
                    Undo.RecordObject(mov, "Pose de la katana");
                    p.brazo = p.muneca = p.corrimiento = Vector3.zero;
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Probar", EditorStyles.boldLabel);
        MovimientoDeKatana.VelocidadDePrueba = EditorGUILayout.Slider("Velocidad", MovimientoDeKatana.VelocidadDePrueba, 0.05f, 1f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tajo")) MovimientoDeKatana.Probar(0);
            if (GUILayout.Button("Tajo fuerte")) MovimientoDeKatana.Probar(1);
            if (GUILayout.Button("Estocada")) MovimientoDeKatana.Probar(2);
        }
        EditorGUILayout.HelpBox("También se puede atacar en el juego: con «Dejar la katana quieta» destildado se mueve como en la partida.", MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("Guardar en el prefab", GUILayout.Height(30f))) Guardar(mov);
        EditorGUILayout.EndScrollView();
    }

    private static Vector3 Deslizadores(Vector3 v, float rango, float paso)
    {
        float Uno(string nombre, float x) => Mathf.Round(EditorGUILayout.Slider(nombre, x, -rango, rango) / paso) * paso;
        return new Vector3(Uno("x", v.x), Uno("y", v.y), Uno("z", v.z));
    }

    private static Vector3 Normalizar(Vector3 grados) => new Vector3(Mathf.DeltaAngle(0f, grados.x), Mathf.DeltaAngle(0f, grados.y), Mathf.DeltaAngle(0f, grados.z));

    // Pasa las poses de la katana en la mano al prefab (Resources/Katana/Katana_ViewModel).
    private static void Guardar(MovimientoDeKatana mov)
    {
        KatanaData katana = KatanaData.Cargar();
        string ruta = katana != null && katana.viewModelPrefab != null ? AssetDatabase.GetAssetPath(katana.viewModelPrefab) : null;
        if (string.IsNullOrEmpty(ruta)) { Debug.LogWarning("Poses de la katana: no encuentro el prefab de la katana."); return; }
        GameObject vista = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            MovimientoDeKatana enPrefab = vista.GetComponent<MovimientoDeKatana>();
            if (enPrefab == null) enPrefab = vista.AddComponent<MovimientoDeKatana>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(mov), enPrefab);
            PrefabUtility.SaveAsPrefabAsset(vista, ruta);
            Debug.Log("Poses de la katana: se guardaron en " + ruta + ".");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(vista);
        }
    }

    private void OnInspectorUpdate() => Repaint();
}
