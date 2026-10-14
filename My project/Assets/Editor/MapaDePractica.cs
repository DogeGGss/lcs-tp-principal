using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// US 118 · Mapa de práctica (Shooting Range). Arma la escena Scenes/Mapa_practica desde el menú de Unity:
// Riftwalker > Crear mapa de práctica. Después es una escena común: se abre y se retoca a mano (luces, decoración).
// Se puede volver a correr: pregunta antes de reemplazar la escena.
// - CA1: una escena aparte de los mapas de partida, solo para practicar (se suma sola al Build Settings).
// - CA2: zona de distancias a la izquierda: tres carriles con rayas en el piso cada 5 m hasta 50 m y un blanco a 10,
//   25 y 50 m, cada uno con su cartel.
// - CA3: zona de blancos a la derecha: blancos a distintas distancias, alturas y costados.
// - CA4: tiene el jugador (con la pausa, que vuelve al menú) y el HUD, sin sala ni rondas: no necesita una partida.
// Medidas en metros. El 0 es la línea de tiro: hacia adelante (+z) los blancos, hacia atrás la zona de salida.
// Piso, paredes y blancos son cubos y cilindros con materiales de color (en Scenes/Mapa_practica/), así se pueden
// cambiar por los del kit Sci-fi sin tocar las medidas.
public static class MapaDePractica
{
    private const string Menu = "Riftwalker/Crear mapa de práctica";
    private const string Ruta = "Assets/Scenes/Mapa_practica.unity";
    private const string Carpeta = "Assets/Scenes/Mapa_practica";
    private const string Jugador = "Assets/Prefabs/Player.prefab";
    private const string Hud = "Assets/UI/HUD/HUDCombate.prefab";

    private const float Ancho = 24f, Atras = 12f, Adelante = 58f, Alto = 6f, Grosor = 0.5f;
    private const float AlturaJugador = 1.15f; // la misma que en Pruebas_Armas: pies en el piso
    private static readonly float[] Carriles = { -9f, -6f, -3f };
    private static readonly int[] DistanciasCarriles = { 10, 25, 50 };
    private const int Largo = 50, CadaRaya = 5;

    private static Material piso, pared, linea, raya, blanco, rojo, poste, caja;

    [MenuItem(Menu)]
    private static void Crear()
    {
        if (EditorApplication.isPlaying) return;
        if (File.Exists(Ruta) && !EditorUtility.DisplayDialog("Mapa de práctica",
                $"Ya existe {Ruta}. ¿Reemplazarla por una nueva? Se pierden los cambios hechos a mano.", "Reemplazar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Materiales();

        Transform mapa = new GameObject("Mapa de práctica").transform;
        Sala(mapa);
        ZonaDeSalida(mapa);
        ZonaDeDistancias(mapa);
        ZonaDeBlancos(mapa);
        Luz();
        if (!Prefab(Jugador, new Vector3(0f, AlturaJugador, -8f)) | !Prefab(Hud, Vector3.zero)) return;

        EditorSceneManager.SaveScene(escena, Ruta);
        SumarAlBuild();
        Debug.Log($"Mapa de práctica: escena guardada en {Ruta} y sumada al Build Settings.");
    }

    // =====================================================================
    // Sala (CA1): cerrada por las cuatro paredes
    // =====================================================================

    private static void Sala(Transform mapa)
    {
        Transform sala = Grupo("Sala", mapa);
        float largo = Atras + Adelante, centroZ = (Adelante - Atras) * 0.5f;
        Caja("Piso", sala, new Vector3(0f, -0.25f, centroZ), new Vector3(Ancho, 0.5f, largo), piso);
        Caja("Pared izquierda", sala, new Vector3(-(Ancho + Grosor) * 0.5f, Alto * 0.5f, centroZ), new Vector3(Grosor, Alto, largo + 2f * Grosor), pared);
        Caja("Pared derecha", sala, new Vector3((Ancho + Grosor) * 0.5f, Alto * 0.5f, centroZ), new Vector3(Grosor, Alto, largo + 2f * Grosor), pared);
        Caja("Pared del fondo", sala, new Vector3(0f, Alto * 0.5f, Adelante + Grosor * 0.5f), new Vector3(Ancho, Alto, Grosor), pared);
        Caja("Pared de atrás", sala, new Vector3(0f, Alto * 0.5f, -Atras - Grosor * 0.5f), new Vector3(Ancho, Alto, Grosor), pared);
    }

    // Detrás de la línea de tiro: donde aparece el jugador.
    private static void ZonaDeSalida(Transform mapa)
    {
        Transform zona = Grupo("Zona de salida", mapa);
        Caja("Línea de tiro", zona, new Vector3(0f, 0.005f, 0f), new Vector3(Ancho, 0.01f, 0.2f), linea, false);
        Texto("Cartel línea de tiro", zona, "LÍNEA DE TIRO", new Vector3(0f, 0.02f, -0.8f), 5f, Color.white, true);
        Texto("Cartel del mapa", zona, "MAPA DE PRÁCTICA", new Vector3(0f, 3.6f, -Atras + 0.05f), 12f, Color.white, false, 180f);
    }

    // =====================================================================
    // Zona de distancias (CA2)
    // =====================================================================

    private static void ZonaDeDistancias(Transform mapa)
    {
        Transform zona = Grupo("Zona de distancias", mapa);
        float izquierda = Carriles[0] - 1.5f, derecha = Carriles[Carriles.Length - 1] + 1.5f;
        float ancho = derecha - izquierda, centro = (izquierda + derecha) * 0.5f;
        Texto("Cartel", zona, "ZONA DE DISTANCIAS", new Vector3(centro, 3.2f, 1.5f), 8f, Color.white, false);

        // Una raya cada 5 m a lo ancho de los carriles, con los metros escritos en el piso.
        for (int m = CadaRaya; m <= Largo; m += CadaRaya)
        {
            bool diez = m % 10 == 0;
            Caja($"{m} m", zona, new Vector3(centro, 0.005f, m), new Vector3(ancho, 0.01f, diez ? 0.15f : 0.08f), diez ? linea : raya, false);
            Texto($"{m} m", zona, $"{m} m", new Vector3(izquierda - 0.9f, 0.02f, m), 4f, diez ? Amarillo : Color.white, true);
        }
        // Los bordes de los carriles.
        for (int i = 0; i <= Carriles.Length; i++)
        {
            float x = izquierda + i * (ancho / Carriles.Length);
            Caja("Borde de carril", zona, new Vector3(x, 0.005f, Largo * 0.5f), new Vector3(0.06f, 0.01f, Largo), raya, false);
        }
        // Un blanco por carril, cada uno a su distancia, así no se tapan entre sí.
        for (int i = 0; i < Carriles.Length; i++)
        {
            int metros = DistanciasCarriles[i];
            Blanco($"Blanco a {metros} m", zona, new Vector3(Carriles[i], 0f, metros), 1.5f);
            Texto($"Cartel {metros} m", zona, $"{metros} m", new Vector3(Carriles[i], 2.6f, metros), 6f, Amarillo, false);
        }
    }

    // =====================================================================
    // Zona de blancos (CA3)
    // =====================================================================

    private static void ZonaDeBlancos(Transform mapa)
    {
        Transform zona = Grupo("Zona de blancos", mapa);
        // Separada de la de distancias por una baranda baja.
        Caja("Baranda", zona, new Vector3(0f, 0.6f, 15f), new Vector3(0.3f, 1.2f, 30f), pared);
        Texto("Cartel", zona, "ZONA DE BLANCOS", new Vector3(6.5f, 3.2f, 1.5f), 8f, Color.white, false);
        // Contorno de la zona en el piso.
        const float x0 = 1.5f, x1 = 11.5f, z0 = 4f, z1 = 30f;
        Caja("Borde", zona, new Vector3((x0 + x1) * 0.5f, 0.005f, z0), new Vector3(x1 - x0, 0.01f, 0.1f), linea, false);
        Caja("Borde", zona, new Vector3((x0 + x1) * 0.5f, 0.005f, z1), new Vector3(x1 - x0, 0.01f, 0.1f), linea, false);
        Caja("Borde", zona, new Vector3(x0, 0.005f, (z0 + z1) * 0.5f), new Vector3(0.1f, 0.01f, z1 - z0), linea, false);
        Caja("Borde", zona, new Vector3(x1, 0.005f, (z0 + z1) * 0.5f), new Vector3(0.1f, 0.01f, z1 - z0), linea, false);

        // Blancos a distintas distancias, alturas y costados: x, z y altura del centro del blanco.
        Vector3[] lugares =
        {
            new Vector3(3.5f, 8f, 1.5f), new Vector3(9.5f, 10f, 1.1f), new Vector3(6.5f, 14f, 1.8f),
            new Vector3(3f, 19f, 1.3f), new Vector3(10f, 22f, 1.6f), new Vector3(6.5f, 27f, 1.5f),
        };
        foreach (Vector3 l in lugares) Blanco($"Blanco {l.y:0} m", zona, new Vector3(l.x, 0f, l.y), l.z);

        // Dos blancos arriba de cajas, para apuntar más alto.
        Caja("Caja", zona, new Vector3(4.5f, 1f, 24f), new Vector3(2f, 2f, 2f), caja);
        Blanco("Blanco alto 24 m", zona, new Vector3(4.5f, 2f, 24f), 1.5f);
        Caja("Caja", zona, new Vector3(9f, 0.75f, 16f), new Vector3(1.5f, 1.5f, 1.5f), caja);
        Blanco("Blanco alto 16 m", zona, new Vector3(9f, 1.5f, 16f), 1.5f);
    }

    // Un blanco redondo sobre un poste, de frente a la línea de tiro (los aros del lado de -z, el poste atrás). "pie"
    // es dónde se apoya el poste y "altura", a cuánto de ahí queda el centro. Solo el tablero tiene colisión (ahí
    // pegan las balas); los aros son el dibujo.
    private static void Blanco(string nombre, Transform padre, Vector3 pie, float altura)
    {
        Transform b = Grupo(nombre, padre);
        b.localPosition = pie;
        Caja("Poste", b, new Vector3(0f, altura * 0.5f, 0.06f), new Vector3(0.08f, altura, 0.08f), poste);
        Caja("Tablero", b, new Vector3(0f, altura, 0f), new Vector3(1f, 1f, 0.06f), blanco);
        float[] aros = { 0.9f, 0.6f, 0.3f };
        for (int i = 0; i < aros.Length; i++)
            Disco($"Aro {i + 1}", b, new Vector3(0f, altura, -0.035f - i * 0.006f), aros[i], i % 2 == 0 ? rojo : blanco);
    }

    // =====================================================================
    // Escena
    // =====================================================================

    private static void Luz()
    {
        var go = new GameObject("Luz", typeof(Light));
        go.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light luz = go.GetComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.2f;
        luz.shadows = LightShadows.Soft;
    }

    private static bool Prefab(string ruta, Vector3 lugar)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Mapa de práctica", $"No se encontró {ruta}: la escena no se guardó.", "Aceptar");
            return false;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = lugar;
        return true;
    }

    private static void SumarAlBuild()
    {
        var escenas = EditorBuildSettings.scenes.ToList();
        if (escenas.Any(e => e.path == Ruta)) return;
        escenas.Add(new EditorBuildSettingsScene(Ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // =====================================================================
    // Piezas
    // =====================================================================

    private static readonly Color Amarillo = new Color(1f, 0.82f, 0.1f);

    private static void Materiales()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Scenes", "Mapa_practica");
        piso = CrearMaterial("Piso", new Color(0.16f, 0.17f, 0.2f));
        pared = CrearMaterial("Pared", new Color(0.32f, 0.34f, 0.38f));
        linea = CrearMaterial("Linea", Amarillo);
        raya = CrearMaterial("Raya", new Color(0.85f, 0.85f, 0.85f));
        blanco = CrearMaterial("Blanco", new Color(0.95f, 0.95f, 0.95f));
        rojo = CrearMaterial("Rojo", new Color(0.85f, 0.15f, 0.15f));
        poste = CrearMaterial("Poste", new Color(0.1f, 0.1f, 0.12f));
        caja = CrearMaterial("Caja", new Color(0.42f, 0.33f, 0.22f));
    }

    // Un material guardado como asset (los que no son assets se pierden al guardar la escena). Si ya existe, se usa
    // ese: así, si alguien le cambió el color o la textura, volver a armar el mapa no lo pisa.
    private static Material CrearMaterial(string nombre, Color color)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    private static Transform Grupo(string nombre, Transform padre)
    {
        Transform t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        return t;
    }

    private static void Caja(string nombre, Transform padre, Vector3 lugar, Vector3 tamano, Material material, bool conColision = true)
    {
        Pieza(PrimitiveType.Cube, nombre, padre, lugar, Quaternion.identity, tamano, material, conColision);
    }

    // Un cilindro aplastado y parado, de frente a -z (como el tablero).
    private static void Disco(string nombre, Transform padre, Vector3 lugar, float diametro, Material material)
    {
        Pieza(PrimitiveType.Cylinder, nombre, padre, lugar, Quaternion.Euler(90f, 0f, 0f),
            new Vector3(diametro, 0.002f, diametro), material, false);
    }

    private static void Pieza(PrimitiveType tipo, string nombre, Transform padre, Vector3 lugar, Quaternion giro,
        Vector3 tamano, Material material, bool conColision)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        if (!conColision) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        go.transform.localRotation = giro;
        go.transform.localScale = tamano;
        go.GetComponent<Renderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
    }

    // Texto 3D. "acostado": sobre el piso, para leerlo desde la línea de tiro. "giroY": hacia dónde mira el cartel
    // (0 = se lee desde la línea de tiro, mirando hacia adelante).
    private static void Texto(string nombre, Transform padre, string texto, Vector3 lugar, float tamano, Color color,
        bool acostado, float giroY = 0f)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        go.transform.localRotation = acostado ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, giroY, 0f);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(30f, 4f);
    }
}
