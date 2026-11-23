using UnityEditor;
using UnityEngine;

// F08 (Modo Invasión): Riftwalker > Invasión > Ver cajas de suministros. Prende y apaga una vista previa en la Scene de
// todos los lugares donde puede caer la caja de suministros (LugarDeSuministros), a la vez:
// - la caja de verdad (ConfigInvasion.cajaPuesto, con su escala), apoyada en el piso, como va a caer;
// - el anillo de carga (ConfigInvasion.radioPuesto) y una línea alta para encontrarla de lejos;
// - el nombre que sale en el aviso («Suministros en el Módulo 4»). En rojo, si tiene un techo encima: ahí la caja no
//   tiene que caer (la caja cae exacta en el punto, así que el punto tiene que estar a cielo abierto).
// No crea objetos en la escena: se dibuja en la vista y sigue los puntos mientras se mueven.
[InitializeOnLoad]
public static class VistaPreviaDeSuministros
{
    private const string Menu = "Riftwalker/Invasión/Ver cajas de suministros";
    private static bool activa;

    static VistaPreviaDeSuministros()
    {
        activa = SessionState.GetBool(Menu, false);
        if (activa) SceneView.duringSceneGui += Dibujar;
        EditorApplication.delayCall += () => UnityEditor.Menu.SetChecked(Menu, activa);
    }

    [MenuItem(Menu)]
    private static void Alternar()
    {
        activa = !activa;
        SessionState.SetBool(Menu, activa);
        SceneView.duringSceneGui -= Dibujar;
        if (activa)
        {
            SceneView.duringSceneGui += Dibujar;
            Encuadrar();
        }
        UnityEditor.Menu.SetChecked(Menu, activa);
        SceneView.RepaintAll();
    }

    // Que entren todas en la vista al prenderla.
    private static void Encuadrar()
    {
        var lugares = Object.FindObjectsByType<LugarDeSuministros>(FindObjectsSortMode.None);
        if (lugares.Length == 0)
        {
            Debug.LogWarning("[Invasión] La escena no tiene lugares de suministros: la caja usa los automáticos (Riftwalker > Invasión > Crear lugares de suministros).");
            return;
        }
        var limites = new Bounds(lugares[0].transform.position, Vector3.one);
        foreach (LugarDeSuministros l in lugares) limites.Encapsulate(l.transform.position);
        limites.Expand(new Vector3(40f, 10f, 40f));
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(limites, false);
    }

    private static void Dibujar(SceneView vista)
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        GameObject modelo = config != null ? config.cajaPuesto : null;
        float escala = config != null ? config.escalaCajaPuesto : 1f;
        float radio = config != null ? config.radioPuesto : 15f;
        var etiqueta = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };

        foreach (LugarDeSuministros l in Object.FindObjectsByType<LugarDeSuministros>(FindObjectsSortMode.None))
        {
            Vector3 p = LugarDeSuministros.Apoyado(l.transform.position);
            bool cielo = PuestoDeSuministros.CieloAbierto(p);
            Color color = cielo ? MarcadorInvasion.Verde : new Color(0.95f, 0.25f, 0.25f);

            if (Event.current.type == EventType.Repaint)
            {
                if (modelo != null) DibujarCaja(modelo, p, escala, vista.camera);
                else Handles.DrawWireCube(p + Vector3.up * 0.45f, new Vector3(1.4f, 0.9f, 1f));
            }
            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawWireDisc(p + Vector3.up * 0.05f, Vector3.up, radio, 2f);
            Handles.DrawLine(p, p + Vector3.up * 12f, 2f);
            etiqueta.normal.textColor = color;
            string nombre = string.IsNullOrWhiteSpace(l.nombre) ? l.name : l.nombre;
            Handles.Label(p + Vector3.up * 13f, cielo ? nombre : nombre + "\n(con techo encima)", etiqueta);
        }
    }

    // La caja con sus materiales, como un objeto más de la escena (sin crearlo).
    private static void DibujarCaja(GameObject modelo, Vector3 lugar, float escala, Camera camara)
    {
        Matrix4x4 caja = Matrix4x4.TRS(lugar, Quaternion.identity, Vector3.one * escala) * modelo.transform.worldToLocalMatrix;
        foreach (MeshFilter filtro in modelo.GetComponentsInChildren<MeshFilter>())
        {
            var dibujo = filtro.GetComponent<MeshRenderer>();
            if (filtro.sharedMesh == null || dibujo == null) continue;
            Matrix4x4 m = caja * filtro.transform.localToWorldMatrix;
            Material[] materiales = dibujo.sharedMaterials;
            for (int i = 0; i < filtro.sharedMesh.subMeshCount && i < materiales.Length; i++)
                if (materiales[i] != null) Graphics.DrawMesh(filtro.sharedMesh, m, materiales[i], 0, camara, i);
        }
    }
}
