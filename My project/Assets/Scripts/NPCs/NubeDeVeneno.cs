using System.Collections.Generic;
using UnityEngine;

// F11 (NPCs), US 267, CA4 y CA5: la nube venenosa que deja la bola de veneno del jefe al caer. Es una nube de humo
// verde (la de la granada de humo, HumoGranada, con otro color): tapa la vista desde afuera y, desde adentro, la
// pantalla se pone verde. Al jugador que está adentro le saca 10 de vida por segundo (de a 5 cada medio segundo). No
// lastima a los enemigos. Dura 10 s (los últimos 2 se va disipando) y como mucho hay 5: si sale una nueva, la más
// vieja se va.
public class NubeDeVeneno : MonoBehaviour
{
    private const float Radio = 4.5f, Dura = 10f;
    private const float Tic = 0.5f;
    private const int DanioPorTic = 5; // 10 por segundo
    private const int Maximo = 5;
    private static readonly Color Verde = new Color(0.42f, 0.86f, 0.28f);          // las partículas
    private static readonly Color Pantalla = new Color(0.3f, 0.55f, 0.2f);         // la pantalla desde adentro

    private static readonly List<NubeDeVeneno> nubes = new List<NubeDeVeneno>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => nubes.Clear();

    private HumoGranada humo;
    private string fuente;
    private float proximoTic;
    private readonly List<HealthSystem> jugadores = new List<HealthSystem>();

    public static void Crear(Vector3 donde, string fuente, Material material)
    {
        nubes.RemoveAll(n => n == null);
        while (nubes.Count >= Maximo) { if (nubes[0] != null) Destroy(nubes[0].gameObject); nubes.RemoveAt(0); }

        HumoGranada humo = HumoGranada.Crear(donde, Radio, Dura, Verde, Pantalla, material);
        humo.gameObject.name = "Nube de veneno";
        var nube = humo.gameObject.AddComponent<NubeDeVeneno>();
        nube.humo = humo;
        nube.fuente = fuente;
        nube.proximoTic = Time.time + Tic;
        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            HealthSystem v = p.GetComponent<HealthSystem>();
            if (v != null) nube.jugadores.Add(v);
        }
        nubes.Add(nube);
    }

    private void Update()
    {
        if (humo == null || Time.time < proximoTic) return;
        proximoTic += Tic;
        if (humo.Opacidad < 0.3f) return; // armándose o ya disipándose
        foreach (HealthSystem vida in jugadores)
        {
            if (vida == null || vida.currentHealth <= 0) continue;
            // Adentro: el cuerpo (de los pies a la cabeza) toca la nube.
            Vector3 pecho = vida.transform.position + Vector3.up * 0.5f;
            if (Vector3.Distance(pecho, humo.Centro) > Radio * 0.9f) continue;
            HealthSystem.DamageOrigin = humo.Centro;
            HealthSystem.DamageSource = fuente;
            vida.TakeDamage(DanioPorTic);
            HealthSystem.DamageSource = null;
        }
    }

    private void OnDestroy() => nubes.Remove(this);
}
