using UnityEditor;
using UnityEngine;

// F11 (NPCs), US 145: para ver los tipos de NPC uno al lado del otro (CA4: que se distingan de lejos). Con el juego
// andando, Riftwalker > NPCs > Aparecer uno de cada tipo los pone en fila adelante de la cámara, y salen a buscar al
// jugador (US 035 y 036). Se borran solos al salir del Play.
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

        TipoDeNpc[] tipos = TipoDeNpc.Todos();
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
}
