using System.Collections.Generic;
using UnityEngine;

// Caerse fuera del mapa (US 006). Va en el jugador de esta computadora: PlayerMovement lo agrega solo.
// - CA1: el jugador está fuera del mapa si entra en una zona marcada (PlayerRespawn, por ejemplo debajo de un
//   vacío) o si baja del límite inferior del mapa: unos metros debajo de lo más bajo que tiene la escena.
// - CA2: en el Táctico cuenta como una muerte sin baja para nadie y espera la ronda siguiente (US 133).
// - CA3: en Deathmatch cuenta como una muerte sin baja para nadie y reaparece a los 3 s (US 137 y US 140).
// - CA4: en Zombie cuenta como una muerte. El fin de la partida es de la US 151 (escucha la muerte del jugador).
// - CA5: fuera de los modos (escenas de prueba), reaparece al instante en el punto de reaparición más cercano.
// - CA6: al reaparecer no conserva la velocidad de la caída (JugadorEnRed.Teletransportar usa PlayerMovement.Frenar),
//   y el punto no choca con el escenario ni con otros personajes.
public class FueraDelMapa : MonoBehaviour
{
    private const float DebajoDelMapa = 10f;  // metros debajo de lo más bajo de la escena
    private const float LimiteAbsoluto = -1000f;

    private float limite = float.NegativeInfinity;
    private bool limiteListo;
    private Pose inicio;
    private CharacterController cuerpo;
    private HealthSystem vida;

    private void Start()
    {
        cuerpo = GetComponent<CharacterController>();
        vida = GetComponent<HealthSystem>();
        inicio = new Pose(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
    }

    private void Update()
    {
        // El límite se calcula un cuadro después, con toda la escena ya cargada.
        if (!limiteListo) { CalcularLimite(); return; }
        if (transform.position.y < limite || transform.position.y < LimiteAbsoluto) Caer(null);
    }

    // CA1: unos metros debajo del colisionador más bajo de la escena (sin contar a los personajes ni sus armas).
    private void CalcularLimite()
    {
        limiteListo = true;
        float minimo = float.PositiveInfinity;
        foreach (Collider c in FindObjectsByType<Collider>())
        {
            if (!c.enabled || c.GetComponentInParent<HealthSystem>() != null || c.GetComponentInParent<ArmaEnPiso>() != null) continue;
            minimo = Mathf.Min(minimo, c.bounds.min.y);
        }
        limite = float.IsPositiveInfinity(minimo) ? LimiteAbsoluto : minimo - DebajoDelMapa;
    }

    /// <summary>
    /// Se cayó fuera del mapa. punto: el punto de reaparición de la zona en la que entró, si tiene (se usa en las
    /// escenas de prueba, junto con los demás, para elegir el más cercano).
    /// </summary>
    public void Caer(Transform punto)
    {
        if (vida != null && vida.currentHealth <= 0) return; // ya está muerto: no cuenta de nuevo

        // CA2 y CA3: en una partida online, muerte sin baja para nadie (JugadorEnRed se ocupa del resto).
        JugadorEnRed enRed = GetComponent<JugadorEnRed>();
        if (enRed != null && PartidaEnRed.Actual != null)
        {
            enRed.Eliminar(JugadorEnRed.Caida);
            return;
        }

        // CA4: en Zombie, muerte.
        if (MapMode.DeEstaEscena == GameMode.Zombie)
        {
            if (vida == null) return;
            vida.Invulnerable = false;
            vida.TakeDamage(vida.currentHealth + vida.currentShield + 1);
            return;
        }

        // CA5 y CA6: en las escenas de prueba, al punto más cercano que esté libre (Teletransportar lo frena).
        Pose destino = PuntoMasCercano(punto);
        JugadorEnRed.Teletransportar(transform, destino.position, destino.rotation);
    }

    // Los puntos de reaparición de la escena (los de las zonas de caída y los de las partidas online) y, si no hay
    // ninguno, donde arrancó el jugador. Gana el más cercano, en horizontal, de los que están libres.
    private Pose PuntoMasCercano(Transform sugerido)
    {
        var puntos = new List<Pose>();
        if (sugerido != null) puntos.Add(PoseDe(sugerido));
        foreach (PlayerRespawn zona in FindObjectsByType<PlayerRespawn>())
            if (zona.spawnPoint != null && zona.spawnPoint != sugerido) puntos.Add(PoseDe(zona.spawnPoint));
        foreach (PuntoDeAparicion punto in FindObjectsByType<PuntoDeAparicion>()) puntos.Add(PoseDe(punto.transform));
        if (puntos.Count == 0) return inicio;

        Vector2 aca = new Vector2(transform.position.x, transform.position.z);
        puntos.Sort((a, b) => Vector2.Distance(aca, new Vector2(a.position.x, a.position.z))
            .CompareTo(Vector2.Distance(aca, new Vector2(b.position.x, b.position.z))));
        foreach (Pose punto in puntos)
            if (Libre(punto.position)) return punto;
        return puntos[0];
    }

    private static Pose PoseDe(Transform t) => new Pose(t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f));

    // CA6: en ese lugar el cuerpo no choca con el escenario ni con otro personaje.
    private bool Libre(Vector3 lugar)
    {
        if (cuerpo == null) return true;
        float radio = cuerpo.radius * 0.9f;
        float medio = Mathf.Max(0f, cuerpo.height * 0.5f - cuerpo.radius);
        Vector3 centro = lugar + cuerpo.center;
        foreach (Collider c in Physics.OverlapCapsule(centro + Vector3.up * medio, centro - Vector3.up * (medio - 0.05f), radio,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(transform)) return false;
        return true;
    }
}
