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
// (Riftwalker > Mapa de práctica: actualizar escena) le cambia o le suma a la escena abierta solo lo de abajo que le
// falte, sin tocar el resto.
// - CA1: una escena aparte de los mapas de partida, solo para practicar (se suma sola al Build Settings).
// - Una sola zona de tiro adelante de la línea de tiro, a todo el ancho: rayas en el piso cada 5 m hasta 50 m con los
//   metros a los dos costados, y cajas para que haya muñecos a distintas alturas. (Antes eran dos zonas, de distancias
//   y de blancos: el menú de actualizar las saca.)
// - CA4: tiene el jugador (con la pausa, que vuelve al menú) y el HUD, sin sala ni rondas: no necesita una partida.
// - US 119: la tienda (B) y el CampoDePractica, que hace que en esta escena todas las armas se elijan gratis.
//   El jugador va sin habilidad (el tutorial presta el Trasbordo durante su paso) y con todas las granadas (US 166).
// - US 120: en la pared derecha, al lado de la línea de tiro, el panel (PanelDePractica) con los modos (bots quietos,
//   en movimiento, distancias y reacción) y reiniciar: pone los muñecos en la zona de tiro. En la línea de tiro, una
//   barrera: un mostrador bajo y, arriba, una pared invisible que frena al jugador pero no a las balas (está en la
//   capa Ignore Raycast, que los disparos no miran).
// - US 166: la guía de controles (TutorialDePractica: carteles de pasos en pantalla, con un botón "Tutorial" en el
//   panel para volver a abrirla) y, en la pared izquierda de la zona de salida, un panel por modo (PanelDeModo) con el
//   objetivo, cómo se gana y cómo se pierde.
// Medidas en metros. El 0 es la línea de tiro: hacia adelante (+z) la zona de tiro, hacia atrás la zona de salida.
// Piso, paredes y cajas son piezas del kit Sci-fi (las mismas del mapa táctico). Debajo de cada pared y del piso queda
// un cubo invisible que es el que choca, así la sala queda cerrada aunque las piezas del kit tengan huecos. Las medidas
// de cada pieza se toman de la pieza misma al armar el mapa (no hace falta saber dónde tiene el centro). Si una pieza
// no está, queda el cubo con su color. Las rayas y el panel son cubos con materiales de color (en Scenes/Mapa_practica/).
public static class MapaDePractica
{
    private const string Menu = "Riftwalker/Crear mapa de práctica";
    private const string MenuActualizar = "Riftwalker/Mapa de práctica: actualizar escena";
    private const string Ruta = "Assets/Scenes/Mapa_practica.unity";
    private const string Carpeta = "Assets/Scenes/Mapa_practica";
    private const string Jugador = "Assets/Prefabs/Player.prefab";
    private const string Hud = "Assets/UI/HUD/HUDCombate.prefab";
    private const string Tienda = "Assets/UI/Tienda/Tienda.prefab"; // US 119: con ella se eligen las armas, gratis
    private const string Munieco = "Assets/Prefabs/MuniecoPrueba.prefab"; // US 120: el bot
    private const string Kit = "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Prefabs";
    private const string KitPiso = Kit + "/Floors/Floor_Squared_01_6x6.prefab";
    private const string KitPared = Kit + "/Walls/Wall_Cube_01_Long.prefab";
    private const string KitCaja = Kit + "/Props/Crate_01.prefab";

    private const float Ancho = 24f, Atras = 12f, Adelante = 58f, Alto = 6f, Grosor = 0.5f;
    private const float AlturaJugador = 1.15f; // la misma que en Pruebas_Armas: pies en el piso
    private const int Largo = 50, CadaRaya = 5;
    private const float ZCarteles = 1.5f;
    private const int CapaSinDisparos = 2; // Ignore Raycast: los disparos (Physics.DefaultRaycastLayers) no la ven

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
        ZonaDeTiro(mapa);
        Barrera(mapa);
        Panel(mapa);
        PanelesDeModos(mapa);
        Luz();
        if (Prefab(Jugador, new Vector3(0f, AlturaJugador, -8f)) == null || Prefab(Hud, Vector3.zero) == null) return;
        if (!ArmasDePractica()) return;
        Tutorial();

        EditorSceneManager.SaveScene(escena, Ruta);
        SumarAlBuild();
        Debug.Log($"Mapa de práctica: escena guardada en {Ruta} y sumada al Build Settings.");
    }

    // Para una escena ya armada y retocada a mano: cambia las dos zonas viejas (distancias y blancos) por la zona de
    // tiro, y le suma la barrera, el panel (US 120), la tienda y el CampoDePractica (US 119) si no los tiene. Lo que se
    // agregó a mano en las zonas viejas (por ejemplo, una pared en el medio) no lo conoce: hay que sacarlo a mano. Hay
    // que tener abierta Scenes/Mapa_practica.
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
        Transform mapa = raiz.transform;
        foreach (string vieja in new[] { "Zona de distancias", "Zona de blancos" })
        {
            Transform zona = mapa.Find(vieja);
            if (zona != null) Object.DestroyImmediate(zona.gameObject);
        }
        if (mapa.Find("Zona de tiro") == null) ZonaDeTiro(mapa);
        if (mapa.Find("Barrera") == null) Barrera(mapa);
        Panel(mapa);
        PanelesDeModos(mapa);
        if (!ArmasDePractica()) return;
        Tutorial();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("Mapa de práctica: escena actualizada (zona de tiro, barrera, panel, tienda, campo de práctica, tutorial y paneles de los modos).");
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
    // Zona de tiro
    // =====================================================================

    // Cajas de la zona de tiro (x, z y alto): así hay muñecos a distintas alturas (PanelDePractica los pone arriba).
    private static readonly Vector3[] CajasDeTiro =
    {
        new Vector3(-6f, 18f, 1.5f), new Vector3(5f, 24f, 2f), new Vector3(-2f, 32f, 1f), new Vector3(8f, 12.5f, 1.5f),
    };

    private static void ZonaDeTiro(Transform mapa)
    {
        Transform zona = Grupo("Zona de tiro", mapa);
        float x0 = -Ancho * 0.5f + 1.5f, x1 = Ancho * 0.5f - 1.5f;
        Cartel(zona, "ZONA DE TIRO", -8f, 8f, ZCarteles);

        // Una raya cada 5 m a todo el ancho, con los metros escritos en el piso a los dos costados.
        for (int m = CadaRaya; m <= Largo; m += CadaRaya)
        {
            bool diez = m % 10 == 0;
            Caja($"{m} m", zona, new Vector3(0f, 0.005f, m), new Vector3(x1 - x0, 0.01f, diez ? 0.15f : 0.08f), diez ? linea : raya, false);
            foreach (float x in new[] { x0 - 0.6f, x1 + 0.6f })
                Texto($"{m} m", zona, $"{m} m", new Vector3(x, 0.02f, m), 4f, diez ? Amarillo : Color.white, true);
        }
        foreach (Vector3 c in CajasDeTiro) CajasDelKit("Cajas", zona, new Vector3(c.x, 0f, c.y), c.z);
    }

    // En la línea de tiro: un mostrador bajo y, arriba, una pared invisible que frena al jugador. Las dos dejan pasar
    // las balas que van por arriba del mostrador (la pared está en la capa que los disparos no miran).
    private static void Barrera(Transform mapa)
    {
        Transform b = Grupo("Barrera", mapa);
        Caja("Mostrador", b, new Vector3(0f, 0.45f, 0.35f), new Vector3(Ancho, 0.9f, 0.4f), poste);
        Caja("Borde del mostrador", b, new Vector3(0f, 0.92f, 0.35f), new Vector3(Ancho, 0.04f, 0.42f), linea, false);
        GameObject invisible = GameObject.CreatePrimitive(PrimitiveType.Cube);
        invisible.name = "Pared invisible";
        invisible.layer = CapaSinDisparos;
        Object.DestroyImmediate(invisible.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(invisible.GetComponent<MeshFilter>());
        invisible.transform.SetParent(b, false);
        invisible.transform.localPosition = new Vector3(0f, (0.9f + Alto) * 0.5f, 0.35f);
        invisible.transform.localScale = new Vector3(Ancho, Alto - 0.9f, 0.4f);
    }

    // =====================================================================
    // US 120: panel
    // =====================================================================

    // El panel de los modos, colgado de la pared derecha al lado de la línea de tiro. Si ya está el de esta versión,
    // solo se ajusta hasta dónde puede haber muñecos; si es uno viejo (o no hay), se arma de nuevo.
    private const string NombrePanel = "Panel de práctica (modos)";
    private const float EscalaPanel = 1.3f; // el panel se arma a 1 y se agranda, con su centro a la misma altura

    private static void Panel(Transform mapa)
    {
        PanelDePractica viejo = mapa.GetComponentInChildren<PanelDePractica>(true);
        // US 166: el de antes de la guía de controles no tiene el botón "Tutorial": se arma de nuevo.
        bool conTutorial = viejo != null && viejo.GetComponentsInChildren<BotonDePractica>(true).Any(b => b.accion == BotonDePractica.Accion.Tutorial);
        if (viejo != null && viejo.name == NombrePanel && conTutorial)
        {
            Limites(viejo, mapa);
            return;
        }
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        foreach (string nombre in new[] { "Bots de práctica", "Lugares de bots", "Lugares de muñecos" })
        {
            Transform resto = mapa.Find(nombre);
            if (resto != null) Object.DestroyImmediate(resto.gameObject);
        }

        // Su -z (el frente) queda hacia -x, hacia adentro de la sala; el 0 de z es la cara de la pared.
        Transform salida = mapa.Find("Zona de salida") ?? mapa;
        Transform p = Grupo(NombrePanel, salida);
        p.SetPositionAndRotation(new Vector3(CaraParedDerecha(mapa), -1.55f * (EscalaPanel - 1f), -2f), Quaternion.Euler(0f, 90f, 0f));
        p.localScale = Vector3.one * EscalaPanel;
        PanelDePractica panel = p.gameObject.AddComponent<PanelDePractica>();
        panel.munieco = AssetDatabase.LoadAssetAtPath<GameObject>(Munieco);
        if (panel.munieco == null) Debug.LogWarning($"Mapa de práctica: no se encontró {Munieco} (el panel queda sin bots).");
        Limites(panel, mapa);

        const float ancho = 2.6f, alto = 1.5f, centroY = 1.55f, marco = 0.06f;
        Caja("Pantalla", p, new Vector3(0f, centroY, -0.04f), new Vector3(ancho, alto, 0.08f), cartel);
        Caja("Marco", p, new Vector3(0f, centroY + (alto - marco) * 0.5f, -0.1f), new Vector3(ancho, marco, 0.04f), linea, false);
        Caja("Marco", p, new Vector3(0f, centroY - (alto - marco) * 0.5f, -0.1f), new Vector3(ancho, marco, 0.04f), linea, false);
        Caja("Marco", p, new Vector3(-(ancho - marco) * 0.5f, centroY, -0.1f), new Vector3(marco, alto, 0.04f), linea, false);
        Caja("Marco", p, new Vector3((ancho - marco) * 0.5f, centroY, -0.1f), new Vector3(marco, alto, 0.04f), linea, false);
        TextoDelPanel("Título", p, "CAMPO DE PRÁCTICA", new Vector3(0f, 2.13f, -0.085f), new Vector2(ancho - 0.3f, 0.2f), Amarillo);
        panel.marcador = TextoDelPanel("Marcador", p, "Bajas: 0", new Vector3(0f, 1.92f, -0.085f), new Vector2(ancho - 0.3f, 0.15f), Color.white);
        Boton(p, "Botón quietos", BotonDePractica.Accion.Quietos, -0.86f, 1.62f);
        Boton(p, "Botón en movimiento", BotonDePractica.Accion.Movimiento, 0f, 1.62f);
        Boton(p, "Botón distancias", BotonDePractica.Accion.Distancias, 0.86f, 1.62f);
        Boton(p, "Botón reacción", BotonDePractica.Accion.Reaccion, -0.86f, 1.24f);
        Boton(p, "Botón reiniciar", BotonDePractica.Accion.Reiniciar, 0f, 1.24f);
        Boton(p, "Botón tutorial", BotonDePractica.Accion.Tutorial, 0.86f, 1.24f); // US 166
        panel.ayuda = TextoDelPanel("Ayuda", p, "Elegí un modo", new Vector3(0f, 0.95f, -0.085f), new Vector2(ancho - 0.3f, 0.12f), Color.white);
    }

    // Entre qué x puede haber muñecos: de pared a pared, sin meterse en el relieve de las paredes del kit. (Además,
    // los que caminan frenan en lo primero sólido que tengan al costado: BotDePractica.)
    private static void Limites(PanelDePractica panel, Transform mapa)
    {
        panel.x0 = -CaraParedDerecha(mapa) + 1f;
        panel.x1 = CaraParedDerecha(mapa) - 1f;
        EditorUtility.SetDirty(panel);
    }

    // Dónde termina, hacia adentro, la pared derecha del kit (sus piezas tienen relieve). Sin piezas, la cara del cubo.
    // La izquierda es igual, del otro lado.
    private static float CaraParedDerecha(Transform mapa)
    {
        Transform kit = mapa.Find("Sala/Kit Sci-fi/Pared derecha (kit)");
        float x = Ancho * 0.5f;
        if (kit != null)
            foreach (MeshRenderer r in kit.GetComponentsInChildren<MeshRenderer>())
                x = Mathf.Min(x, r.bounds.min.x);
        return x;
    }

    // Un botón amarillo sobre la pantalla del panel, que se ilumina al mirarlo y si es el modo elegido. No es estático:
    // cambia de material. El texto lo pone el panel.
    private static void Boton(Transform panel, string nombre, BotonDePractica.Accion accion, float x, float y)
    {
        GameObject go = Pieza(PrimitiveType.Cube, nombre, panel, new Vector3(x, y, -0.11f), Quaternion.identity,
            new Vector3(0.8f, 0.3f, 0.06f), linea, true);
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        BotonDePractica boton = go.AddComponent<BotonDePractica>();
        boton.accion = accion;
        boton.normal = linea;
        boton.resaltado = raya;
        boton.texto = TextoDelPanel("Texto " + nombre, panel, "", new Vector3(x, y, -0.145f), new Vector2(0.72f, 0.17f),
            new Color(0.07f, 0.09f, 0.13f));
    }

    // Texto del panel que se achica solo para entrar en su recuadro (en metros).
    private static TextMeshPro TextoDelPanel(string nombre, Transform padre, string texto, Vector3 lugar, Vector2 recuadro, Color color)
    {
        TextMeshPro t = Texto(nombre, padre, texto, lugar, 2f, color, false);
        t.rectTransform.sizeDelta = recuadro;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.5f;
        t.fontSizeMax = 2f;
        return t;
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

    // =====================================================================
    // US 166: guía de controles y paneles de los modos
    // =====================================================================

    private const string Fuentes = "Assets/UI/Fuentes";
    private const string Redondeado = "Assets/UI/Tienda/Sprites/Redondeado.png";
    private const string HabilidadDePrueba = "Assets/Data/Habilidades/Trasbordo.asset"; // la que presta el paso de la Q

    // Los carteles de los pasos (se arman en pantalla al jugar). Si ya está, solo se le ponen las tipografías.
    private static void Tutorial()
    {
        TutorialDePractica t = Object.FindAnyObjectByType<TutorialDePractica>(FindObjectsInactive.Include);
        if (t == null) t = new GameObject("Tutorial (guía de controles)").AddComponent<TutorialDePractica>();
        t.displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{Fuentes}/BarlowCondensed-Bold SDF.asset");
        t.labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{Fuentes}/BarlowCondensed-SemiBold SDF.asset");
        t.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{Fuentes}/Barlow-Regular SDF.asset");
        t.rounded = AssetDatabase.LoadAssetAtPath<Sprite>(Redondeado);
        t.habilidadDePrueba = AssetDatabase.LoadAssetAtPath<AbilityData>(HabilidadDePrueba);
        EditorUtility.SetDirty(t);
    }

    private const string NombrePanelesDeModos = "Paneles de los modos";

    // CA3: lo que dice cada panel. Se puede cambiar después en el Inspector de cada uno (PanelDeModo).
    private static readonly (string modo, string objetivo, string gana, string pierde)[] Modos =
    {
        ("TÁCTICO",
         "4 contra 4 por rondas, sin reaparecer. Los atacantes plantan el dispositivo en A o B ({Plantar}) y los defensores lo defienden o lo desactivan. {Correr} camina despacio y sin hacer ruido (no se corre). La habilidad de tu personaje se usa con {Habilidad} y las granadas se compran en la fase de compra.",
         "Ganás la ronda si el dispositivo explota, si lo desactivás o si eliminás a todo el otro equipo (si se acaba el tiempo sin plantar, ganan los defensores). Gana la partida el primer equipo en llegar a 7 rondas.",
         "Pierde el equipo que no llega a 7 rondas. Si morís, mirás a un compañero hasta la ronda siguiente."),
        ("DEATHMATCH",
         "Todos contra todos, hasta 8 jugadores. Elegís tus armas gratis ({Tienda}), cada baja te cura 50 y reaparecés a los 3 s. Tu habilidad se usa con {Habilidad}. No hay escudos ni granadas.",
         "Gana el primero en llegar a 25 bajas, o el que más bajas tenga cuando se terminan los 8 minutos.",
         "Perdés si otro llega antes a 25 bajas o tiene más bajas que vos al terminar el reloj."),
        ("ZOMBIE",
         "Vos solo contra la horda en la universidad, durante 10 oleadas. En la oleada 10 aparece el jefe. Con los puntos que ganás comprás armas, granadas y munición entre oleadas.",
         "Ganás si sobrevivís a las 10 oleadas. Después podés seguir en el modo infinito para hacer récord.",
         "Perdés si los zombis te matan."),
    };

    // CA3: un panel por modo, colgados de la pared izquierda de la zona de salida, de frente a la sala. Si ya están, solo
    // se les ponen los textos de arriba (lo cambiado a mano en el Inspector se pisa).
    private static void PanelesDeModos(Transform mapa)
    {
        Transform salida = mapa.Find("Zona de salida") ?? mapa;
        Transform hechos = salida.Find(NombrePanelesDeModos);
        if (hechos != null)
        {
            for (int i = 0; i < Modos.Length; i++)
            {
                Transform t = hechos.Find("Panel " + Modos[i].modo.ToLowerInvariant());
                PanelDeModo hecho = t != null ? t.GetComponent<PanelDeModo>() : null;
                if (hecho == null) continue;
                hecho.objetivo = Modos[i].objetivo;
                hecho.comoSeGana = Modos[i].gana;
                hecho.comoSePierde = Modos[i].pierde;
                EditorUtility.SetDirty(hecho);
            }
            return;
        }
        Transform grupo = Grupo(NombrePanelesDeModos, salida);
        float pared = -CaraParedDerecha(mapa); // la sala es pareja: la izquierda está del otro lado
        float[] zs = { -7f, -4.3f, -1.6f };
        for (int i = 0; i < Modos.Length; i++)
        {
            // Su -z (el frente) queda hacia +x, hacia adentro de la sala.
            Transform p = Grupo("Panel " + Modos[i].modo.ToLowerInvariant(), grupo);
            p.SetPositionAndRotation(new Vector3(pared, 0f, zs[i]), Quaternion.Euler(0f, -90f, 0f));
            PanelDeModo panel = p.gameObject.AddComponent<PanelDeModo>();
            panel.objetivo = Modos[i].objetivo;
            panel.comoSeGana = Modos[i].gana;
            panel.comoSePierde = Modos[i].pierde;

            const float ancho = 2.4f, alto = 2.7f, centroY = 1.85f, marco = 0.06f;
            Caja("Pantalla", p, new Vector3(0f, centroY, -0.04f), new Vector3(ancho, alto, 0.08f), cartel);
            Caja("Marco", p, new Vector3(0f, centroY + (alto - marco) * 0.5f, -0.1f), new Vector3(ancho, marco, 0.04f), linea, false);
            Caja("Marco", p, new Vector3(0f, centroY - (alto - marco) * 0.5f, -0.1f), new Vector3(ancho, marco, 0.04f), linea, false);
            Caja("Marco", p, new Vector3(-(ancho - marco) * 0.5f, centroY, -0.1f), new Vector3(marco, alto, 0.04f), linea, false);
            Caja("Marco", p, new Vector3((ancho - marco) * 0.5f, centroY, -0.1f), new Vector3(marco, alto, 0.04f), linea, false);
            panel.titulo = TextoDelPanel("Título", p, Modos[i].modo, new Vector3(0f, 2.95f, -0.085f), new Vector2(ancho - 0.3f, 0.3f), Amarillo);
            panel.acercate = TextoDelPanel("Acercate", p, "Acercate para ver el objetivo", new Vector3(0f, 1.85f, -0.085f),
                new Vector2(ancho - 0.4f, 0.14f), Color.white);

            // El detalle: varios renglones, arriba a la izquierda, achicándose solo para entrar.
            TextMeshPro d = Texto("Detalle", p, "", new Vector3(0f, 1.72f, -0.085f), 1f, Color.white, false);
            d.rectTransform.sizeDelta = new Vector2(ancho - 0.3f, 2.1f);
            d.fontStyle = FontStyles.Normal;
            d.alignment = TextAlignmentOptions.TopLeft;
            d.textWrappingMode = TextWrappingModes.Normal;
            d.enableAutoSizing = true;
            d.fontSizeMin = 0.4f;
            d.fontSizeMax = 1.2f;
            panel.detalle = d;
        }
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
