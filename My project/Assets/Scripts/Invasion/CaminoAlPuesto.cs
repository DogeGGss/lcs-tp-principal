using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// F08 (Modo Invasión), US 335: el camino en el piso hasta el puesto de suministros, como el de Overwatch cuando marca
// dónde defender o atacar. Lo agrega PuestoDeSuministros.
// - CA8: en el descanso, flechas verdes (el color del puesto) que avanzan desde el jugador hasta el próximo puesto,
//   por el camino caminando (el NavMesh, como los enemigos: esquiva paredes y sube escaleras). Se recalcula mientras
//   el jugador se mueve, se ve un poco a través de las paredes (Efectos/Camino) y desaparece al entrar al anillo.
//   Para que sea fluido, el camino se calcula del puesto al jugador (las flechas quedan fijas en el piso, ancladas al
//   puesto) en cada cuadro, y los puntos van siempre cada 0,8 m contados desde el puesto.
// - CA9: con la oleada empezada, si el jugador está fuera del anillo sigue apareciendo hasta el puesto, más tenue.
public class CaminoAlPuesto : MonoBehaviour
{
    private const float Paso = 0.8f;          // m entre puntos (para que siga el piso)
    private const float Ancho = 0.7f, SobreElPiso = 0.07f;
    private const float Desde = 1.2f;         // m: arranca un poco adelante de los pies
    private const float EnElDescanso = 0.95f, EnLaOleada = 0.45f; // opacidad (CA8 y CA9)

    private PuestoDeSuministros puesto;
    private OleadasDeInvasion oleadas;
    private LineRenderer linea;
    private Material material;
    private NavMeshPath camino;
    private readonly List<Vector3> puntos = new List<Vector3>();

    private void Awake()
    {
        puesto = GetComponent<PuestoDeSuministros>();
        oleadas = GetComponent<OleadasDeInvasion>();
        camino = new NavMeshPath();
    }

    private void OnDestroy()
    {
        if (linea != null) Destroy(linea.gameObject);
        if (material != null) Destroy(material);
    }

    private void Update()
    {
        float opacidad = Opacidad();
        if (opacidad <= 0f)
        {
            if (linea != null && linea.enabled) linea.enabled = false;
            return;
        }
        if (linea == null && !Armar()) return;
        Calcular();
        linea.enabled = puntos.Count >= 2;
        Color c = MarcadorInvasion.Verde;
        c.a = opacidad;
        linea.startColor = linea.endColor = c;
    }

    // CA8: en el descanso, hasta el próximo (o el que está); CA9: en la oleada, si está fuera del anillo, más tenue.
    private float Opacidad()
    {
        if (puesto == null || !puesto.HayDestino || puesto.AdentroDelDestino) return 0f;
        if (ResultadoDeInvasion.Actual != null || (oleadas != null && oleadas.EnElFinal)) return 0f;
        if (puesto.ElJugador == null) return 0f;
        bool descanso = oleadas == null || oleadas.Descanso;
        return descanso ? EnElDescanso : EnLaOleada;
    }

    private bool Armar()
    {
        Shader shader = Resources.Load<Shader>("Efectos/Camino");
        if (shader == null) shader = Shader.Find("Efectos/Camino");
        if (shader == null) return false;
        material = new Material(shader) { name = "Camino al puesto" };
        material.SetColor("_Color", Color.white); // el color va por el LineRenderer

        var go = new GameObject("Camino al puesto");
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // acostada: mira para arriba
        linea = go.AddComponent<LineRenderer>();
        linea.sharedMaterial = material;
        linea.useWorldSpace = true;
        linea.alignment = LineAlignment.TransformZ;
        linea.textureMode = LineTextureMode.Tile;
        linea.widthMultiplier = Ancho;
        linea.numCornerVertices = 3;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
        linea.enabled = false;
        return true;
    }

    // El camino caminando del puesto al jugador, partido en pasos cortos pegados al piso (cada 0,8 m desde el puesto,
    // así los puntos no se corren al caminar).
    private void Calcular()
    {
        puntos.Clear();
        Transform yo = puesto.ElJugador;
        int tipo = NavMeshEnVivo.Listo ? NavMeshEnVivo.Tipo : NavMesh.GetSettingsByIndex(0).agentTypeID;
        var filtro = new NavMeshQueryFilter { agentTypeID = tipo, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(yo.position, out NavMeshHit desde, 4f, filtro)) { linea.positionCount = 0; return; }
        if (!NavMesh.SamplePosition(puesto.Destino, out NavMeshHit hasta, 4f, filtro)) { linea.positionCount = 0; return; }
        if (!NavMesh.CalculatePath(hasta.position, desde.position, filtro, camino) || camino.status == NavMeshPathStatus.PathInvalid)
        {
            linea.positionCount = 0;
            return;
        }

        Vector3[] esquinas = camino.corners;
        float total = 0f;
        for (int i = 0; i + 1 < esquinas.Length; i++) total += Vector3.Distance(esquinas[i], esquinas[i + 1]);
        float hastaDonde = total - Desde; // corta un poco antes de los pies
        if (hastaDonde <= 0f || esquinas.Length < 2) { linea.positionCount = 0; return; }

        // Las esquinas (sin perderlas) y, entre ellas, los puntos cada 0,8 m contados desde el puesto.
        float recorrido = 0f;
        puntos.Add(AlPiso(esquinas[0]));
        for (int i = 0; i + 1 < esquinas.Length && recorrido < hastaDonde; i++)
        {
            Vector3 a = esquinas[i], b = esquinas[i + 1];
            float largo = Vector3.Distance(a, b);
            float siguiente = (Mathf.Floor(recorrido / Paso) + 1f) * Paso;
            while (siguiente < recorrido + largo && siguiente < hastaDonde)
            {
                puntos.Add(AlPiso(Vector3.Lerp(a, b, (siguiente - recorrido) / largo)));
                siguiente += Paso;
            }
            if (recorrido + largo >= hastaDonde)
            {
                puntos.Add(AlPiso(Vector3.Lerp(a, b, (hastaDonde - recorrido) / largo)));
                break;
            }
            puntos.Add(AlPiso(b));
            recorrido += largo;
        }
        linea.positionCount = puntos.Count;
        for (int i = 0; i < puntos.Count; i++) linea.SetPosition(i, puntos[i]);
    }

    // Pegado al piso de verdad (el NavMesh va un poco arriba o abajo en las escaleras y rampas).
    private static Vector3 AlPiso(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit piso, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && piso.collider.GetComponentInParent<Npc>() == null && piso.collider.GetComponentInParent<PlayerMovement>() == null)
            p.y = piso.point.y;
        return p + Vector3.up * SobreElPiso;
    }
}
