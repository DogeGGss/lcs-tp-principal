using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// US 022, CA5: el perro del Callejero corre por el piso siguiendo los pasillos, las puertas, las escaleras y las rampas,
// sin atravesar paredes. Para eso hace falta un NavMesh del mapa. En lugar de prepararlo a mano en cada escena, se arma
// solo con el juego andando, a partir de las colisiones del mapa (sin personajes, triggers ni objetos que se mueven),
// la primera vez que alguien tiene la habilidad. Se arma de a poco (asíncrono) para no trabar el juego.
public static class NavMeshEnVivo
{
    private const float Alcance = 160f; // m alrededor del jugador: un terreno enorme no se procesa entero

    private static NavMeshData datos;
    private static NavMeshDataInstance instancia;
    private static AsyncOperation armando;
    private static UnityEngine.SceneManagement.Scene escena;
    private static int tipo = -1;

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
        datos = null;
        armando = null;
        instancia = default;
        escena = default;
    }

    /// <summary>El tipo de agente (tamaño de perro) del NavMesh armado.</summary>
    public static int Tipo => tipo;

    /// <summary>Empieza a armarlo para la escena actual, si todavía no se armó.</summary>
    public static void Preparar(Vector3 centro)
    {
        UnityEngine.SceneManagement.Scene actual = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (datos != null && escena == actual && instancia.valid) return;
        if (instancia.valid) instancia.Remove();
        escena = actual;

        NavMeshBuildSettings ajustes = NavMesh.CreateSettings();
        ajustes.agentRadius = 0.3f;
        ajustes.agentHeight = 0.9f;
        ajustes.agentClimb = 0.45f;   // escalones
        ajustes.agentSlope = 46f;     // rampas y escaleras (US 008)
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.1f;
        tipo = ajustes.agentTypeID;

        Bounds limites = new Bounds(centro, new Vector3(Alcance * 2f, 80f, Alcance * 2f));
        var fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(limites, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), fuentes);
        fuentes.RemoveAll(f => !DelMapa(f.component as Collider));

        datos = new NavMeshData(tipo);
        instancia = NavMesh.AddNavMeshData(datos);
        armando = NavMeshBuilder.UpdateNavMeshDataAsync(datos, ajustes, fuentes, limites);
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
        !PuertaAutomatica.EsPuerta(c);
}
