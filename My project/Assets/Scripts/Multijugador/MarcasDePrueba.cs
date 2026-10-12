using UnityEngine;

// Dibujos de las herramientas de prueba del editor (ReglaTrasbordo, RadiosGranada): líneas, círculos y carteles
// en el mundo, sin luz, que se borran solos. En una build no existe.
public static class MarcasDePrueba
{
#if UNITY_EDITOR
    private static Material material;

    /// <summary>Un grupo vacío que se borra solo a los "segundos"; las marcas se cuelgan de ahí.</summary>
    public static GameObject Grupo(string nombre, float segundos)
    {
        var go = new GameObject(nombre);
        Object.Destroy(go, segundos);
        return go;
    }

    public static void Linea(GameObject grupo, Vector3 desde, Vector3 hasta, float ancho, Color color)
    {
        LineRenderer linea = NuevaLinea(grupo, "Línea", ancho, color);
        linea.positionCount = 2;
        linea.SetPosition(0, desde);
        linea.SetPosition(1, hasta);
    }

    public static void Circulo(GameObject grupo, Vector3 centro, float radio, float ancho, Color color)
    {
        LineRenderer linea = NuevaLinea(grupo, "Círculo", ancho, color);
        const int puntos = 64;
        linea.loop = true;
        linea.positionCount = puntos;
        for (int i = 0; i < puntos; i++)
        {
            float a = i * Mathf.PI * 2f / puntos;
            linea.SetPosition(i, centro + new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio));
        }
    }

    /// <summary>Un cartel de "alto" metros de letra, de frente a la cámara del momento.</summary>
    public static void Cartel(GameObject grupo, Vector3 lugar, string texto, float alto, Color color)
    {
        var go = new GameObject("Cartel");
        go.transform.SetParent(grupo.transform, false);
        go.transform.position = lugar;
        TextMesh cartel = go.AddComponent<TextMesh>();
        Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cartel.font = fuente;
        cartel.text = texto;
        cartel.fontSize = 64;
        cartel.characterSize = alto * 16f / 64f; // alto aproximado de la letra, en metros
        cartel.anchor = TextAnchor.LowerCenter;
        cartel.alignment = TextAlignment.Center;
        cartel.color = color;
        if (fuente != null) go.GetComponent<MeshRenderer>().sharedMaterial = fuente.material;
        Camera camara = Camera.main;
        if (camara != null) go.transform.rotation = Quaternion.LookRotation(lugar - camara.transform.position);
    }

    /// <summary>El piso debajo de "punto" (o el mismo punto si no hay nada abajo).</summary>
    public static Vector3 Piso(Vector3 punto)
    {
        return Physics.Raycast(punto + Vector3.up * 0.2f, Vector3.down, out RaycastHit golpe, 5f, ~0, QueryTriggerInteraction.Ignore)
            ? golpe.point : punto;
    }

    private static LineRenderer NuevaLinea(GameObject grupo, string nombre, float ancho, Color color)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(grupo.transform, false);
        LineRenderer linea = go.AddComponent<LineRenderer>();
        if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Marcas de prueba" };
        linea.sharedMaterial = material;
        linea.useWorldSpace = true;
        linea.widthMultiplier = ancho;
        linea.startColor = linea.endColor = color;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
        return linea;
    }
#endif
}
