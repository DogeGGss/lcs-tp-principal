using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// US 022, CA5: el perro del Callejero corre por el piso siguiendo los pasillos, las puertas, las escaleras y las rampas,
// sin atravesar paredes. Para eso hace falta un NavMesh del mapa. En lugar de prepararlo a mano en cada escena, se arma
// solo con el juego andando, a partir de las colisiones del mapa (sin personajes, triggers ni objetos que se mueven),
// la primera vez que alguien tiene la habilidad. Se arma de a poco (asíncrono) para no trabar el juego.
// Se arman dos: uno para los cuerpos chicos (el perro, los enemigos) y otro para los grandes (el Jefe, US 146), que no
// pasa por puertas ni pasillos angostos ni entra a los edificios (si no, embistiendo o volando se metía y se trababa).
public static class NavMeshEnVivo
{
    // Hasta dónde se arma: todo el mapa (las colisiones del mapa, sin el terreno, más un margen), así no quedan zonas
    // sin NavMesh en los mapas grandes (en la uni, a más de 160 m de donde se empezó, los enemigos se trababan contra un
    // "muro invisible" y el camino al puesto no salía). El terreno no cuenta para medir: uno enorme no se procesa entero.
    private const float Margen = 20f, MaximoDeLado = 900f, MaximoDeAlto = 200f;

    private const float RadioGrande = 1.6f, AltoGrande = 3f;

    private static NavMeshData datos, datosGrande;
    private static NavMeshDataInstance instancia, instanciaGrande;
    private static AsyncOperation armando, armandoGrande;
    private static UnityEngine.SceneManagement.Scene escena;
    private static int tipo = -1, tipoGrande = -1;

    /// <summary>Ya se puede usar (terminó de armarse para la escena actual).</summary>
    public static bool Listo => datos != null && armando != null && armando.isDone && instancia.valid &&
                                escena == UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    // El proyecto entra al Play sin recargar los scripts ni la escena (Enter Play Mode Options): al salir del Play
    // Unity borra el NavMesh armado, pero esto seguía creyendo que estaba (y no lo volvía a armar: en la segunda
    // partida los NPCs y el perro no se movían). Se olvida al empezar cada partida.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Olvidar()
    {
        if (instancia.valid) instancia.Remove(); // por si quedó (no se suma otro encima)
        if (instanciaGrande.valid) instanciaGrande.Remove();
        datos = datosGrande = null;
        armando = armandoGrande = null;
        instancia = instanciaGrande = default;
        escena = default;
    }

    /// <summary>El tipo de agente (tamaño de perro) del NavMesh armado.</summary>
    public static int Tipo => tipo;

    /// <summary>El de los cuerpos grandes (el Jefe) ya se puede usar.</summary>
    public static bool ListoGrande => Listo && datosGrande != null && armandoGrande != null && armandoGrande.isDone && instanciaGrande.valid;

    /// <summary>El tipo de agente de los cuerpos grandes (el Jefe): no pasa por lugares angostos.</summary>
    public static int TipoGrande => tipoGrande;

    /// <summary>Empieza a armarlo para la escena actual, si todavía no se armó.</summary>
    public static void Preparar(Vector3 centro)
    {
        UnityEngine.SceneManagement.Scene actual = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (datos != null && escena == actual && instancia.valid) return;
        if (instancia.valid) instancia.Remove();
        if (instanciaGrande.valid) instanciaGrande.Remove();
        escena = actual;

        NavMeshBuildSettings ajustes = NavMesh.CreateSettings();
        ajustes.agentRadius = 0.3f;
        ajustes.agentHeight = 0.9f;
        ajustes.agentClimb = 0.45f;   // escalones
        ajustes.agentSlope = 46f;     // rampas y escaleras (US 008)
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.1f;
        tipo = ajustes.agentTypeID;

        Bounds limites = Limites(centro);
        var fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(limites, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), fuentes);
        // El terreno (el pasto de la uni, por ejemplo) no viene como un Collider sino como Terrain: antes se descartaba y
        // el NavMesh quedaba solo sobre las veredas y los pisos, así los NPCs y el perro no pisaban el pasto.
        fuentes.RemoveAll(f => f.shape != NavMeshBuildSourceShape.Terrain && !DelMapa(f.component as Collider));

        datos = new NavMeshData(tipo);
        instancia = NavMesh.AddNavMeshData(datos);
        armando = NavMeshBuilder.UpdateNavMeshDataAsync(datos, ajustes, fuentes, limites);

        // El de los grandes, con las mismas colisiones.
        NavMeshBuildSettings grande = NavMesh.CreateSettings();
        grande.agentRadius = RadioGrande;
        grande.agentHeight = AltoGrande;
        grande.agentClimb = 0.45f;
        grande.agentSlope = 46f;
        grande.overrideVoxelSize = true;
        grande.voxelSize = 0.2f;
        tipoGrande = grande.agentTypeID;
        datosGrande = new NavMeshData(tipoGrande);
        instanciaGrande = NavMesh.AddNavMeshData(datosGrande);
        armandoGrande = NavMeshBuilder.UpdateNavMeshDataAsync(datosGrande, grande, fuentes, limites);
    }

    // Todo el mapa: la caja de todas sus colisiones (sin el terreno ni lo que no es mapa), con un margen y un tope.
    private static Bounds Limites(Vector3 centro)
    {
        bool hay = false;
        Bounds b = new Bounds(centro, Vector3.one);
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c is TerrainCollider || !DelMapa(c)) continue;
            Bounds cb = c.bounds;
            if (cb.size.x > MaximoDeLado || cb.size.z > MaximoDeLado) continue; // un plano gigante (el agua, un piso infinito)
            if (!hay) { b = cb; hay = true; } else b.Encapsulate(cb);
        }
        b.Encapsulate(centro);
        b.Expand(new Vector3(Margen * 2f, 10f, Margen * 2f));
        // El tope, alrededor del jugador.
        Vector3 min = Vector3.Max(b.min, centro - new Vector3(MaximoDeLado, MaximoDeAlto, MaximoDeLado) * 0.5f);
        Vector3 max = Vector3.Min(b.max, centro + new Vector3(MaximoDeLado, MaximoDeAlto, MaximoDeLado) * 0.5f);
        b.SetMinMax(min, max);
        return b;
    }

    // Lo que cuenta como mapa: colisiones sólidas que no son de personajes ni se mueven con física. Las barreras
    // (US 020) tampoco: duran poco y el perro las rodea igual porque las choca. Las puertas automáticas tampoco: se abren
    // cuando el perro llega. Los muros de las bases (US 190) tampoco: están solo en la compra, y el NavMesh se arma justo
    // ahí (al aparecer con la habilidad), así que quedaban horneados y el perro se trababa en el borde de cada base toda
    // la partida.
    private static bool DelMapa(Collider c) =>
        c != null && c.enabled && !c.isTrigger &&
        (c.attachedRigidbody == null || c.attachedRigidbody.isKinematic) &&
        c.GetComponentInParent<HealthSystem>() == null && c.GetComponentInParent<CharacterController>() == null &&
        c.GetComponentInParent<Barrera>() == null && c.GetComponentInParent<Perro>() == null &&
        c.GetComponentInParent<ArmaEnPiso>() == null && c.GetComponentInParent<MurosDeBase>() == null &&
        c.GetComponentInParent<PortalDeEnemigos>() == null && // US 269: duran una oleada
        !PuertaAutomatica.EsPuerta(c);
}
