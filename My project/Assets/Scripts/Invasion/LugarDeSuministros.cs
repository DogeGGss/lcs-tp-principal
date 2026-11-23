using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// F08 (Modo Invasión), US 292: un lugar del mapa donde puede caer el puesto de suministros, con el nombre que sale en
// el aviso («Suministros en el Módulo 4»). Se ponen a mano en la escena (Riftwalker > Invasión > Crear lugares de
// suministros los arma en los módulos y la biblioteca para moverlos después). Si la escena no tiene ninguno, el puesto
// los arma solo al empezar con la misma regla (Automaticos).
public class LugarDeSuministros : MonoBehaviour
{
    [Tooltip("Cómo se nombra el lugar en el aviso, con el artículo: «el Módulo 4», «la Biblioteca».")]
    public string nombre = "el Módulo 1";

    private static readonly List<LugarDeSuministros> todos = new List<LugarDeSuministros>();

    /// <summary>Los lugares activos de la escena.</summary>
    public static IReadOnlyList<LugarDeSuministros> Todos => todos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => todos.Clear();

    private void OnEnable() => todos.Add(this);
    private void OnDisable() => todos.Remove(this);

    private static readonly Regex Modulo = new Regex(@"^Modulo_?(\d+)([A-Za-z])?(?:_|$)");

    /// <summary>El nombre del lugar para un objeto del mapa de la uni ("Modulo_4" → "el Módulo 4"), o null.</summary>
    public static string NombreDe(string objeto)
    {
        Match m = Modulo.Match(objeto);
        if (m.Success) return L10n.T("gameplay.place.module_fmt", m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
        if (objeto.StartsWith("Biblioteca")) return L10n.T("gameplay.place.library");
        if (objeto == "Patio_Bar") return L10n.T("gameplay.place.bar"); // el patio de la galería del Módulo 9
        return null;
    }

    private static readonly Regex ModuloConArticulo = new Regex(@"^el Módulo (\w+)$");

    /// <summary>El nombre de un lugar puesto a mano (en español, con el artículo: «el Auditorio», «el Módulo 7») en el
    /// idioma del juego. Los edificios del mapa están en el catálogo (gameplay.place.*); uno que no está queda igual.</summary>
    public static string Traducido(string nombre)
    {
        if (L10n.TrySourceKey(nombre, out string clave, "gameplay.place.")) return L10n.T(clave);
        Match m = ModuloConArticulo.Match(nombre ?? "");
        return m.Success ? L10n.T("gameplay.place.module_fmt", m.Groups[1].Value) : nombre;
    }

    /// <summary>Un lugar por módulo (y la biblioteca y el bar) a partir de los nombres de los objetos del mapa: el
    /// centro de lo que se dibuja de cada uno, a la altura del piso.</summary>
    public static List<(string nombre, Vector3 lugar)> Automaticos()
    {
        var limites = new Dictionary<string, Bounds>();
        var orden = new List<string>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string nombre = NombreDe(t.name);
            if (nombre == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (limites.TryGetValue(nombre, out Bounds b)) { b.Encapsulate(r.bounds); limites[nombre] = b; }
                else { limites[nombre] = r.bounds; orden.Add(nombre); }
            }
        }
        var lugares = new List<(string, Vector3)>();
        foreach (string nombre in orden)
        {
            Bounds b = limites[nombre];
            lugares.Add((nombre, new Vector3(b.center.x, b.min.y + 0.5f, b.center.z)));
        }
        return lugares;
    }

    /// <summary>El punto apoyado en el piso que tiene abajo (por si quedó un poco arriba o abajo al moverlo a mano).</summary>
    public static Vector3 Apoyado(Vector3 punto)
    {
        return Physics.Raycast(punto + Vector3.up * 0.75f, Vector3.down, out RaycastHit piso, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? piso.point : punto;
    }

    /// <summary>Cerca de "centro", el primer punto a cielo abierto apoyado en el piso (buscando en anillos de 2 m hasta
    /// "hasta"), o null. Para armar los lugares en el editor, donde todavía no hay NavMesh.</summary>
    public static Vector3? ACieloAbierto(Vector3 centro, float hasta)
    {
        for (float r = 0f; r <= hasta; r += 2f)
        {
            int pasos = r <= 0f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 2f);
            for (int k = 0; k < pasos; k++)
            {
                float a = k * Mathf.PI * 2f / pasos;
                Vector3 p = centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                // Desde arriba, lo primero que se toca es el piso (no un techo): a la altura del suelo del lugar.
                if (!Physics.Raycast(new Vector3(p.x, centro.y + 80f, p.z), Vector3.down, out RaycastHit piso, 90f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (piso.point.y > centro.y + 1f || Vector3.Angle(piso.normal, Vector3.up) > 20f) continue;
                if (PuestoDeSuministros.CieloAbierto(piso.point)) return piso.point;
            }
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.49f, 0.88f, 0.35f, 0.9f);
        Vector3 p = transform.position;
        Gizmos.DrawWireCube(p + Vector3.up * 0.45f, new Vector3(1.4f, 0.9f, 1f));
        Gizmos.DrawLine(p, p + Vector3.up * 4f);
    }
}
