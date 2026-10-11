using UnityEngine;

// US 019 · Trasbordo, solo para probar el CA1 (9 m) en el editor. Se prende y apaga desde el menú de Unity:
// Riftwalker > Prueba: regla del Trasbordo. Con la regla prendida, cada salto del jugador de esta computadora deja
// en el piso, durante unos segundos, una línea de 9 m desde donde salió con una marca por metro, una cruz donde
// llegó y un cartel con los metros que recorrió (también sale en la Consola). Funciona en cualquier mapa.
// En una build no existe.
public static class ReglaTrasbordo
{
#if UNITY_EDITOR
    private const string Clave = "Riftwalker.PruebaReglaTrasbordo";
    private const string Menu = "Riftwalker/Prueba: regla del Trasbordo";
    private const float Duracion = 10f;
    private static readonly Color ColorRegla = new Color(1f, 0.85f, 0.2f);
    private static readonly Color ColorLlegada = new Color(0.25f, 1f, 0.5f);

    private static GameObject actual;
    private static Material material;

    private static bool Activa => UnityEditor.EditorPrefs.GetBool(Clave, false);

    [UnityEditor.MenuItem(Menu)]
    private static void Cambiar()
    {
        UnityEditor.EditorPrefs.SetBool(Clave, !Activa);
        Debug.Log("Prueba: regla del Trasbordo: " + (Activa ? "prendida" : "apagada"));
    }

    [UnityEditor.MenuItem(Menu, true)]
    private static bool Marcar()
    {
        UnityEditor.Menu.SetChecked(Menu, Activa);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Suscribir()
    {
        HabilidadTrasbordo.Usado -= Dibujar; // por si no se recarga el dominio al darle Play
        HabilidadTrasbordo.Usado += Dibujar;
    }

    private static void Dibujar(Transform jugador, Vector3 origen, Vector3 destino)
    {
        if (!Activa || jugador == null) return;
        Vector3 plano = destino - origen;
        plano.y = 0f;
        float metros = plano.magnitude;
        Debug.Log($"Trasbordo: recorrió {metros:0.00} m (CA1 pide {HabilidadTrasbordo.Distancia:0} m)");
        if (metros < 0.01f) return;

        if (actual != null) Object.Destroy(actual);
        actual = new GameObject("Regla del Trasbordo (prueba)");
        Object.Destroy(actual, Duracion);

        CharacterController cuerpo = jugador.GetComponent<CharacterController>();
        float pies = cuerpo != null ? (cuerpo.center.y - cuerpo.height * 0.5f) * jugador.lossyScale.y : -1f;
        Vector3 direccion = plano / metros;
        Vector3 costado = Vector3.Cross(Vector3.up, direccion);
        Vector3 inicio = origen + Vector3.up * (pies + 0.03f);
        float largo = HabilidadTrasbordo.Distancia;

        Linea(inicio, inicio + direccion * largo, 0.06f, ColorRegla);
        for (int m = 0; m <= largo; m++)
        {
            float media = m == largo ? 0.6f : m % 5 == 0 ? 0.4f : 0.25f;
            Vector3 punto = inicio + direccion * m;
            Linea(punto - costado * media, punto + costado * media, 0.05f, ColorRegla);
            if (m > 0) Cartel(punto + Vector3.up * 0.3f, m + " m", 0.2f, ColorRegla);
        }

        Vector3 llegada = destino + Vector3.up * (pies + 0.05f);
        Vector3 diagonal1 = (direccion + costado) * 0.4f, diagonal2 = (direccion - costado) * 0.4f;
        Linea(llegada - diagonal1, llegada + diagonal1, 0.08f, ColorLlegada);
        Linea(llegada - diagonal2, llegada + diagonal2, 0.08f, ColorLlegada);
        Cartel(llegada + Vector3.up * 2.4f, $"{metros:0.00} m", 0.4f, ColorLlegada);
    }

    private static void Linea(Vector3 desde, Vector3 hasta, float ancho, Color color)
    {
        var go = new GameObject("Línea");
        go.transform.SetParent(actual.transform, false);
        LineRenderer linea = go.AddComponent<LineRenderer>();
        linea.sharedMaterial = MaterialLineas();
        linea.useWorldSpace = true;
        linea.positionCount = 2;
        linea.SetPosition(0, desde);
        linea.SetPosition(1, hasta);
        linea.widthMultiplier = ancho;
        linea.startColor = linea.endColor = color;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
    }

    private static void Cartel(Vector3 lugar, string texto, float tamano, Color color)
    {
        var go = new GameObject("Cartel");
        go.transform.SetParent(actual.transform, false);
        go.transform.position = lugar;
        TextMesh cartel = go.AddComponent<TextMesh>();
        Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cartel.font = fuente;
        cartel.text = texto;
        cartel.fontSize = 64;
        cartel.characterSize = tamano * 16f / 64f; // alto aproximado de la letra, en metros
        cartel.anchor = TextAnchor.LowerCenter;
        cartel.alignment = TextAlignment.Center;
        cartel.color = color;
        if (fuente != null) go.GetComponent<MeshRenderer>().sharedMaterial = fuente.material;
        // De frente a la cámara del momento del salto (mira hacia donde saltó).
        Camera camara = Camera.main;
        if (camara != null) go.transform.rotation = Quaternion.LookRotation(lugar - camara.transform.position);
    }

    private static Material MaterialLineas()
    {
        if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Regla del Trasbordo" };
        return material;
    }
#endif
}
