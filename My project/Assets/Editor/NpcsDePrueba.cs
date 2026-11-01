using UnityEditor;
using UnityEngine;

// F11 (NPCs), US 145: para ver los tipos de NPC uno al lado del otro (CA4: que se distingan de lejos). Con el juego
// andando, Riftwalker > NPCs > Aparecer uno de cada tipo los pone en fila adelante de la cámara, y salen a buscar al
// jugador (US 035 y 036). Los jefes (US 146) no entran en la fila: salen con Riftwalker > NPCs > Aparecer el jefe, un
// poco más lejos. Los que atacan a distancia (los Watcher, US 285 y US 286) salen solos con Riftwalker > NPCs > Aparecer
// los Watcher, más lejos (para que disparen). Se borran solos al salir del Play.
public static class NpcsDePrueba
{
    [MenuItem("Riftwalker/NPCs/Aparecer uno de cada tipo")]
    private static void Aparecer()
    {
        Camera cam = Camera.main;
        Transform desde = cam != null ? cam.transform : null;
        Vector3 adelante = desde != null ? Vector3.ProjectOnPlane(desde.forward, Vector3.up).normalized : Vector3.forward;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        Vector3 costado = Vector3.Cross(Vector3.up, adelante);
        Vector3 centro = (desde != null ? desde.position : Vector3.zero) + adelante * 8f;

        TipoDeNpc[] tipos = System.Array.FindAll(TipoDeNpc.Todos(), t => !t.esJefe);
        for (int i = 0; i < tipos.Length; i++)
        {
            Vector3 lugar = centro + costado * ((i - (tipos.Length - 1) * 0.5f) * 4f);
            // Al piso, si hay piso abajo.
            if (Physics.Raycast(lugar + Vector3.up * 5f, Vector3.down, out RaycastHit piso, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                lugar = piso.point;
            Npc npc = Npc.Crear(tipos[i], lugar, Quaternion.LookRotation(-adelante, Vector3.up));
            if (npc != null)
                Debug.Log($"NPC de prueba: {tipos[i].nombre} ({tipos[i].vida} de vida, golpe {tipos[i].danio}, {tipos[i].velocidad} m/s, escala {tipos[i].escala}).");
        }
        if (tipos.Length == 0) Debug.LogWarning("No hay tipos de NPC en Resources/NPCs.");
    }

    [MenuItem("Riftwalker/NPCs/Aparecer uno de cada tipo", true)]
    private static bool SePuede() => Application.isPlaying;

    [MenuItem("Riftwalker/NPCs/Aparecer el jefe")]
    private static void AparecerJefe()
    {
        TipoDeNpc jefe = System.Array.Find(TipoDeNpc.Todos(), t => t.esJefe);
        if (jefe == null) { Debug.LogWarning("No hay ningún tipo de NPC marcado como jefe en Resources/NPCs."); return; }
        Camera cam = Camera.main;
        Transform desde = cam != null ? cam.transform : null;
        Vector3 adelante = desde != null ? Vector3.ProjectOnPlane(desde.forward, Vector3.up).normalized : Vector3.forward;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        Vector3 lugar = (desde != null ? desde.position : Vector3.zero) + adelante * 14f;
        if (Physics.Raycast(lugar + Vector3.up * 5f, Vector3.down, out RaycastHit piso, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            lugar = piso.point;
        if (Npc.Crear(jefe, lugar, Quaternion.LookRotation(-adelante, Vector3.up)) != null)
            Debug.Log($"Jefe de prueba: {jefe.nombre} ({jefe.vida} de vida, golpe {jefe.danio}, {jefe.velocidad} m/s, escala {jefe.escala}).");
    }

    [MenuItem("Riftwalker/NPCs/Aparecer el jefe", true)]
    private static bool SePuedeJefe() => Application.isPlaying;

    [MenuItem("Riftwalker/NPCs/Aparecer los Watcher")]
    private static void AparecerWatchers()
    {
        TipoDeNpc[] tipos = System.Array.FindAll(TipoDeNpc.Todos(), t => !t.esJefe && t.aDistancia != AtaqueADistancia.Ninguno);
        if (tipos.Length == 0) { Debug.LogWarning("No hay tipos de NPC que ataquen a distancia en Resources/NPCs."); return; }
        Camera cam = Camera.main;
        Transform desde = cam != null ? cam.transform : null;
        Vector3 adelante = desde != null ? Vector3.ProjectOnPlane(desde.forward, Vector3.up).normalized : Vector3.forward;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        Vector3 costado = Vector3.Cross(Vector3.up, adelante);
        Vector3 centro = (desde != null ? desde.position : Vector3.zero) + adelante * 15f; // dentro de su distancia de disparo
        for (int i = 0; i < tipos.Length; i++)
        {
            Vector3 lugar = centro + costado * ((i - (tipos.Length - 1) * 0.5f) * 5f);
            if (Physics.Raycast(lugar + Vector3.up * 5f, Vector3.down, out RaycastHit piso, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                lugar = piso.point;
            TipoDeNpc t = tipos[i];
            if (Npc.Crear(t, lugar, Quaternion.LookRotation(-adelante, Vector3.up)) != null)
                Debug.Log($"Watcher de prueba: {t.nombre} ({t.aDistancia}, {t.danioADistancia} de daño cada {t.cadaCuantoDispara} s, dispara entre {t.distanciaMinima} y {t.distanciaMaxima} m).");
        }
    }

    [MenuItem("Riftwalker/NPCs/Aparecer los Watcher", true)]
    private static bool SePuedenWatchers() => Application.isPlaying;
}
