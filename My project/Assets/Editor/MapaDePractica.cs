using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// US 118 · Mapa de práctica (Shooting Range). Arma la escena Scenes/Mapa_practica desde el menú de Unity:
// Riftwalker > Crear mapa de práctica. Después es una escena común: se abre y se retoca a mano (luces, decoración).
// Se puede volver a correr: pregunta antes de reemplazar la escena. Para no perder lo retocado a mano, el otro menú
// (Riftwalker > Mapa de práctica: actualizar escena) le suma a la escena abierta solo lo que le falte: los carteles,
// la habilidad apagada y lo de la US 119 (la tienda y el CampoDePractica).
// - CA1: una escena aparte de los mapas de partida, solo para practicar (se suma sola al Build Settings).
// - CA2: zona de distancias a la izquierda: tres carriles con rayas en el piso cada 5 m hasta 50 m y un blanco a 10,
//   25 y 50 m, cada uno con su cartel.
// - CA3: zona de blancos a la derecha: blancos a distintas distancias, alturas y costados.
// - CA4: tiene el jugador (con la pausa, que vuelve al menú) y el HUD, sin sala ni rondas: no necesita una partida.
// - US 119: la tienda (B) y el CampoDePractica, que hace que en esta escena todas las armas se elijan gratis.
//   El jugador va sin habilidad (el mapa es para practicar con las armas), así el HUD tampoco la muestra.
// Medidas en metros. El 0 es la línea de tiro: hacia adelante (+z) los blancos, hacia atrás la zona de salida.
// Piso, paredes y cajas son piezas del kit Sci-fi (las mismas del mapa táctico). Debajo de cada pared y del piso queda
// un cubo invisible que es el que choca, así la sala queda cerrada aunque las piezas del kit tengan huecos. Las medidas
// de cada pieza se toman de la pieza misma al armar el mapa (no hace falta saber dónde tiene el centro). Si una pieza
// no está, queda el cubo con su color. Los blancos y las rayas son cubos y cilindros con materiales de color (en
// Scenes/Mapa_practica/).
public static class MapaDePractica
{
    private const string Menu = "Riftwalker/Crear mapa de práctica";
    private const string MenuActualizar = "Riftwalker/Mapa de práctica: actualizar escena";
    private const string Ruta = "Assets/Scenes/Mapa_practica.unity";
    private const string Carpeta = "Assets/Scenes/Mapa_practica";
    private const string Jugador = "Assets/Prefabs/Player.prefab";
    private const string Hud = "Assets/UI/HUD/HUDCombate.prefab";
    private const string Tienda = "Assets/UI/Tienda/Tienda.prefab"; // US 119: con ella se eligen las armas, gratis
    private const string Kit = "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Prefabs";
    private const string KitPiso = Kit + "/Floors/Floor_Squared_01_6x6.prefab";
    private const string KitPared = Kit + "/Walls/Wall_Cube_01_Long.prefab";
    private const string KitCaja = Kit + "/Props/Crate_01.prefab";

    private const float Ancho = 24f, Atras = 12f, Adelante = 58f, Alto = 6f, Grosor = 0.5f;
    private const float AlturaJugador = 1.15f; // la misma que en Pruebas_Armas: pies en el piso
    private static readonly float[] Carriles = { -9f, -6f, -3f };
    private static readonly int[] DistanciasCarriles = { 10, 25, 50 };
    private const int Largo = 50, CadaRaya = 5;
    private static float DistanciasX0 => Carriles[0] - 1.5f;
    private static float DistanciasX1 => Carriles[Carriles.Length - 1] + 1.5f;
    private const float BlancosX0 = 1.5f, BlancosX1 = 11.5f, ZCarteles = 1.5f;

    private static Material piso, pared, linea, raya, blanco, rojo, poste, caja, cartel;

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
        if (Prefab(Jugador, new Vector3(0f, AlturaJugador, -8f)) == null || Prefab(Hud, Vector3.zero) == null) return;
        SacarHabilidad();
        if (!ArmasDePractica()) return;

        EditorSceneManager.SaveScene(escena, Ruta);
        SumarAlBuild();
        Debug.Log($"Mapa de práctica: escena guardada en {Ruta} y sumada al Build Settings.");
    }

    // Para una escena ya armada y retocada a mano: le pone los carteles con marco, le saca la habilidad al jugador y le
    // suma la tienda y el CampoDePractica (US 119) si no los tiene, sin tocar nada más. Hay que tener abierta
    // Scenes/Mapa_practica.
    [MenuItem(MenuActualizar)]
    private static void Actualizar()
    {
        if (EditorApplication.isPlaying) return;
        Scene escena = SceneManager.GetActiveScene();
        GameObject raiz = escena.path == Ruta ? escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mapa de práctica") : null;
        if (raiz == null)
        {
            EditorUtility.DisplayDialog("Mapa de práctica", $"Abrí primero {Ruta} (con su grupo \"Mapa de práctica\").", "Aceptar");
            return;
        }
        Materiales();
        CambiarCartel(raiz.transform.Find("Zona de distancias"), "ZONA DE DISTANCIAS", DistanciasX0, DistanciasX1);
        CambiarCartel(raiz.transform.Find("Zona de blancos"), "ZONA DE BLANCOS", BlancosX0, BlancosX1);
        SacarHabilidad();
        if (!ArmasDePractica()) return;
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("Mapa de práctica: escena actualizada (carteles, sin habilidad, tienda y campo de práctica).");
    }

    // Saca el cartel viejo (solo el texto) y pone el nuevo. Si la zona ya tiene el nuevo (con tablero), no hace nada.
    private static void CambiarCartel(Transform zona, string texto, float x0, float x1)
    {
        if (zona == null) return;
        Transform viejo = zona.Find("Cartel");
        if (viejo != null && viejo.Find("Tablero") != null) return;
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        Cartel(zona, texto, x0, x1, ZCarteles);
    }

    // El jugador va sin habilidad: en la copia de la escena (el prefab no cambia) PlayerAbility queda sin habilidad y sin
    // tomar la del personaje elegido. Sin habilidad la Q no hace nada y el HUD esconde el chip solo. Se cambian los
    // campos en vez de sacar el componente porque Unity no deja borrar componentes de un prefab desde un script.
    private static void SacarHabilidad()
    {
        foreach (PlayerAbility habilidad in Object.FindObjectsByType<PlayerAbility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var campos = new SerializedObject(habilidad);
            campos.FindProperty("ability").objectReferenceValue = null;
            campos.FindProperty("useSelectedCharacter").boolValue = false;
            campos.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // US 119: el CampoDePractica (todas las armas gratis) y la tienda, que es donde se eligen. Si ya están, no se tocan.
    private static bool ArmasDePractica()
    {
        if (Object.FindAnyObjectByType<CampoDePractica>(FindObjectsInactive.Include) == null)
            new GameObject("Campo de práctica", typeof(CampoDePractica));
        if (Object.FindAnyObjectByType<ShopUI>(FindObjectsInactive.Include) == null && Prefab(Tienda, Vector3.zero) == null)
            return false;
        return true;
    }

    // =====================================================================
    // Sala (CA1): cerrada por las cuatro paredes
    // =====================================================================

    private static void Sala(Transform mapa)
    {
        Transform sala = Grupo("Sala", mapa);
        float largo = Atras + Adelante, centroZ = (Adelante - Atras) * 0.5f;
        // Los cubos que chocan. Se ven solo si falta la pieza del kit.
        bool conPiso = AssetDatabase.LoadAssetAtPath<GameObject>(KitPiso) != null;
        bool conPared = AssetDatabase.LoadAssetAtPath<GameObject>(KitPared) != null;
        Caja("Piso", sala, new Vector3(0f, -0.25f, centroZ), new Vector3(Ancho, 0.5f, largo), piso, true, !conPiso);
        Caja("Pared izquierda", sala, new Vector3(-(Ancho + Grosor) * 0.5f, Alto * 0.5f, centroZ), new Vector3(Grosor, Alto, largo + 2f * Grosor), pared, true, !conPared);
        Caja("Pared derecha", sala, new Vector3((Ancho + Grosor) * 0.5f, Alto * 0.5f, centroZ), new Vector3(Grosor, Alto, largo + 2f * Grosor), pared, true, !conPared);
        Caja("Pared del fondo", sala, new Vector3(0f, Alto * 0.5f, Adelante + Grosor * 0.5f), new Vector3(Ancho, Alto, Grosor), pared, true, !conPared);
        Caja("Pared de atrás", sala, new Vector3(0f, Alto * 0.5f, -Atras - Grosor * 0.5f), new Vector3(Ancho, Alto, Grosor), pared, true, !conPared);

        // Lo que se ve: baldosas del kit en el piso y piezas de pared del kit contra cada pared, de adentro.
        Transform kit = Grupo("Kit Sci-fi", sala);
        PisoDelKit(kit, -Ancho * 0.5f, Ancho * 0.5f, -Atras, Adelante);
        ParedDelKit("Pared izquierda (kit)", kit, Vector3.forward, new Vector3(-Ancho * 0.5f, 0f, -Atras), largo, Vector3.right);
        ParedDelKit("Pared derecha (kit)", kit, Vector3.forward, new Vector3(Ancho * 0.5f, 0f, -Atras), largo, Vector3.left);
        ParedDelKit("Pared del fondo (kit)", kit, Vector3.right, new Vector3(-Ancho * 0.5f, 0f, Adelante), Ancho, Vector3.back);
        ParedDelKit("Pared de atrás (kit)", kit, Vector3.right, new Vector3(-Ancho * 0.5f, 0f, -Atras), Ancho, Vector3.forward);
    }

    // =====================================================================
    // Piezas del kit Sci-fi
    // =====================================================================

    // Cuánto ocupa una pieza girada "giro" grados (en y) con su centro en el 0: así se acomoda sin saber dónde está su
    // centro. Se miden solo las mallas (no las luces ni las partículas que traen algunas piezas).
    private static Bounds Medir(GameObject prefab, float giro)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, giro, 0f));
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool hay = false;
        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>())
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        Object.DestroyImmediate(go);
        return b;
    }

    private static GameObject Poner(GameObject prefab, string nombre, Transform padre, Vector3 lugar, float giro)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.name = nombre;
        go.transform.SetPositionAndRotation(lugar, Quaternion.Euler(0f, giro, 0f));
        return go;
    }

    // Baldosas del kit cubriendo el rectángulo, con la cara de arriba en y = 0.
    private static void PisoDelKit(Transform padre, float x0, float x1, float z0, float z1)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPiso);
        if (prefab == null) return;
        Bounds b = Medir(prefab, 0f);
        if (b.size.x < 0.5f || b.size.z < 0.5f) return;
        Transform grupo = Grupo("Piso", padre);
        for (float x = x0; x < x1 - 0.01f; x += b.size.x)
            for (float z = z0; z < z1 - 0.01f; z += b.size.z)
                Poner(prefab, "Baldosa", grupo, new Vector3(x - b.min.x, -b.max.y, z - b.min.z), 0f);
    }

    // Piezas de pared del kit a lo largo de una pared: desde "inicio", "largo" metros en la dirección "a lo largo",
    // con la cara contra la línea de la pared y apiladas hasta la altura de la sala. "adentro" apunta al centro de la
    // sala: la pieza se gira para que su lado largo quede a lo largo de la pared y su cara mire hacia adentro.
    private static void ParedDelKit(string nombre, Transform padre, Vector3 aLoLargo, Vector3 inicio, float largo, Vector3 adentro)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPared);
        if (prefab == null) return;
        // De los cuatro giros, el que deja el lado largo a lo largo de la pared. Entre los dos que sirven, el que mira
        // hacia adentro: así las cuatro paredes muestran la misma cara a la sala.
        float giro = Mathf.Atan2(adentro.x, adentro.z) * Mathf.Rad2Deg;
        Bounds b = Medir(prefab, giro);
        if (Mathf.Abs(Vector3.Dot(b.size, aLoLargo)) < Mathf.Abs(Vector3.Dot(b.size, adentro)))
        {
            giro += 90f;
            b = Medir(prefab, giro);
        }
        float paso = Mathf.Abs(Vector3.Dot(b.size, aLoLargo)), alto = b.size.y;
        if (paso < 0.5f || alto < 0.5f) return;
        // Qué parte de la pieza queda contra la pared: su lado más lejos del centro de la sala.
        Vector3 haciaAfuera = -adentro;
        float fondo = Vector3.Dot(b.center, haciaAfuera) + Mathf.Abs(Vector3.Dot(b.extents, haciaAfuera));
        float desdeInicio = Vector3.Dot(b.min, aLoLargo) < Vector3.Dot(b.max, aLoLargo)
            ? Vector3.Dot(b.min, aLoLargo) : Vector3.Dot(b.max, aLoLargo);

        Transform grupo = Grupo(nombre, padre);
        for (float y = 0f; y < Alto - 0.01f; y += alto)
            for (float d = 0f; d < largo - 0.01f; d += paso)
            {
                Vector3 lugar = inicio + aLoLargo * (d - desdeInicio) - haciaAfuera * fondo + Vector3.up * (y - b.min.y);
                Poner(prefab, "Pared", grupo, lugar, giro);
            }
    }

    // Cajas del kit apiladas hasta llegar al menos a "altura". Devuelve dónde queda la cara de arriba.
    private static float CajasDelKit(string nombre, Transform padre, Vector3 pie, float altura)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitCaja);
        if (prefab == null)
        {
            Caja(nombre, padre, pie + Vector3.up * altura * 0.5f, new Vector3(2f, altura, 2f), caja);
            return pie.y + altura;
        }
        Bounds b = Medir(prefab, 0f);
        if (b.size.y < 0.1f) return pie.y;
        Transform grupo = Grupo(nombre, padre);
        float y = pie.y;
        while (y < pie.y + altura - 0.01f)
        {
            Poner(prefab, "Caja", grupo, new Vector3(pie.x - b.center.x, y - b.min.y, pie.z - b.center.z), 0f);
            y += b.size.y;
        }
        return y;
    }

    // Detrás de la línea de tiro: donde aparece el jugador.
    private static void ZonaDeSalida(Transform mapa)
    {
        Transform zona = Grupo("Zona de salida", mapa);
        Caja("Línea de tiro", zona, new Vector3(0f, 0.005f, 0f), new Vector3(Ancho, 0.01f, 0.2f), linea, false);
        Texto("Cartel línea de tiro", zona, "LÍNEA DE TIRO", new Vector3(0f, 0.02f, -0.8f), 5f, Color.white, true);
        CajasDelKit("Cajas", zona, new Vector3(-10f, 0f, -10f), 1.5f);
        CajasDelKit("Cajas", zona, new Vector3(10f, 0f, -10f), 0.7f);
        Texto("Cartel del mapa", zona, "MAPA DE PRÁCTICA", new Vector3(0f, 3.6f, -Atras + 0.05f), 12f, Color.white, false, 180f);
    }

    // =====================================================================
    // Zona de distancias (CA2)
    // =====================================================================

    private static void ZonaDeDistancias(Transform mapa)
    {
        Transform zona = Grupo("Zona de distancias", mapa);
        float izquierda = DistanciasX0, derecha = DistanciasX1;
        float ancho = derecha - izquierda, centro = (izquierda + derecha) * 0.5f;
        Cartel(zona, "ZONA DE DISTANCIAS", izquierda, derecha, ZCarteles);

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
        // Contorno de la zona en el piso.
        const float x0 = BlancosX0, x1 = BlancosX1, z0 = 4f, z1 = 30f;
        Cartel(zona, "ZONA DE BLANCOS", x0, x1, ZCarteles);
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
        float arriba = CajasDelKit("Cajas", zona, new Vector3(4.5f, 0f, 24f), 2f);
        Blanco("Blanco alto 24 m", zona, new Vector3(4.5f, arriba, 24f), 1.5f);
        arriba = CajasDelKit("Cajas", zona, new Vector3(9f, 0f, 16f), 1.5f);
        Blanco("Blanco alto 16 m", zona, new Vector3(9f, arriba, 16f), 1.5f);
    }

    // El cartel con el nombre de la zona: un tablero oscuro con marco amarillo, sobre dos postes, a lo ancho de la zona
    // (de x0 a x1) y de frente a la línea de tiro. El texto se achica solo para entrar en el tablero.
    private static void Cartel(Transform padre, string texto, float x0, float x1, float z)
    {
        const float centroY = 3.6f, alto = 1.1f, marco = 0.08f;
        float ancho = x1 - x0;
        Transform c = Grupo("Cartel", padre);
        c.localPosition = new Vector3((x0 + x1) * 0.5f, 0f, z);
        float tope = centroY + alto * 0.5f;
        foreach (float lado in new[] { -1f, 1f })
            Caja("Poste", c, new Vector3(lado * (ancho * 0.5f - 0.2f), tope * 0.5f, 0.1f), new Vector3(0.15f, tope, 0.15f), poste);
        Caja("Tablero", c, new Vector3(0f, centroY, 0f), new Vector3(ancho, alto, 0.1f), cartel);
        // El marco, apenas delante del tablero.
        const float zMarco = -0.07f;
        Caja("Marco", c, new Vector3(0f, centroY + (alto - marco) * 0.5f, zMarco), new Vector3(ancho, marco, 0.04f), linea, false);
        Caja("Marco", c, new Vector3(0f, centroY - (alto - marco) * 0.5f, zMarco), new Vector3(ancho, marco, 0.04f), linea, false);
        Caja("Marco", c, new Vector3(-(ancho - marco) * 0.5f, centroY, zMarco), new Vector3(marco, alto, 0.04f), linea, false);
        Caja("Marco", c, new Vector3((ancho - marco) * 0.5f, centroY, zMarco), new Vector3(marco, alto, 0.04f), linea, false);

        TextMeshPro t = Texto("Texto", c, texto, new Vector3(0f, centroY, -0.08f), 8f, Color.white, false);
        t.rectTransform.sizeDelta = new Vector2(ancho - 0.6f, alto - 0.25f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = 2f;
        t.fontSizeMax = 8f;
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

    private static GameObject Prefab(string ruta, Vector3 lugar)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Mapa de práctica", $"No se encontró {ruta}: la escena no se guardó.", "Aceptar");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = lugar;
        return go;
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
        cartel = CrearMaterial("Cartel", new Color(0.07f, 0.09f, 0.13f));
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

    private static void Caja(string nombre, Transform padre, Vector3 lugar, Vector3 tamano, Material material,
        bool conColision = true, bool visible = true)
    {
        GameObject go = Pieza(PrimitiveType.Cube, nombre, padre, lugar, Quaternion.identity, tamano, material, conColision);
        go.GetComponent<Renderer>().enabled = visible;
    }

    // Un cilindro aplastado y parado, de frente a -z (como el tablero).
    private static void Disco(string nombre, Transform padre, Vector3 lugar, float diametro, Material material)
    {
        Pieza(PrimitiveType.Cylinder, nombre, padre, lugar, Quaternion.Euler(90f, 0f, 0f),
            new Vector3(diametro, 0.002f, diametro), material, false);
    }

    private static GameObject Pieza(PrimitiveType tipo, string nombre, Transform padre, Vector3 lugar, Quaternion giro,
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
        return go;
    }

    // Texto 3D. "acostado": sobre el piso, para leerlo desde la línea de tiro. "giroY": hacia dónde mira el cartel
    // (0 = se lee desde la línea de tiro, mirando hacia adelante).
    private static TextMeshPro Texto(string nombre, Transform padre, string texto, Vector3 lugar, float tamano, Color color,
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
        return t;
    }
}
