using System.Collections.Generic;
using UnityEngine;

// Punto de plantado del Modo Táctico (US 131, CA1): el dispositivo solo se puede plantar dentro de una de estas
// zonas. Cada mapa tiene dos, A y B. La zona es el collider (se vuelve trigger solo); lo que se ve en el piso es
// aparte (por ejemplo, el cuadrado rojo del prefab ZonaDePlantado).
[RequireComponent(typeof(Collider))]
public class ZonaDePlantado : MonoBehaviour
{
    private static readonly List<ZonaDePlantado> zonas = new List<ZonaDePlantado>();

    [Tooltip("Nombre del punto, como lo ven los jugadores: A o B.")]
    public string nombre = "A";

    private Collider area;

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
        PonerLetra();
    }

    // La letra pintada en el piso (si la zona tiene un texto) es su nombre.
    private void OnValidate() => PonerLetra();

    private void PonerLetra()
    {
        foreach (TMPro.TMP_Text letra in GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (letra.text != nombre) letra.text = nombre;
    }

    private void OnEnable() => zonas.Add(this);
    private void OnDisable() => zonas.Remove(this);

    /// <summary>La zona en la que está ese punto (los pies del jugador), o null. Solo mira el piso de la zona.</summary>
    public static ZonaDePlantado En(Vector3 punto)
    {
        foreach (ZonaDePlantado zona in zonas)
        {
            if (zona.area == null) continue;
            Bounds limites = zona.area.bounds;
            // Un poco por debajo del piso de la zona también cuenta (los pies quedan justo en el borde).
            Vector3 p = new Vector3(punto.x, Mathf.Clamp(punto.y, limites.min.y, limites.max.y), punto.z);
            if (punto.y >= limites.min.y - 0.5f && punto.y <= limites.max.y && limites.Contains(p)) return zona;
        }
        return null;
    }
}
