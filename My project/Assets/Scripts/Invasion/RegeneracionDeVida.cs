using UnityEngine;

// F08 (Modo Invasión), US 149: la vida se regenera sola, como en CoD Zombies. La pone PartidaDeInvasion en el jugador,
// así que solo existe en este modo (CA6).
// - CA1: empieza cuando el jugador pasa 4 s seguidos sin recibir daño.
// - CA2: mientras se regenera recupera 8 de vida por segundo, de a poco (se junta lo de cada cuadro), hasta la vida
//   máxima (100).
// - CA3: cualquier golpe, aunque solo le saque escudo, corta la regeneración y la espera arranca de nuevo.
// - CA4: el escudo no se toca: solo se recupera comprándolo.
// - CA5: los bordes rojos de vida baja (US 058, CombatHud) se van aclarando mientras sube la vida.
// Con el juego en pausa el tiempo se detiene, así que la espera y la regeneración también.
[RequireComponent(typeof(HealthSystem))]
public class RegeneracionDeVida : MonoBehaviour
{
    public const float Espera = 4f;          // CA1
    public const float VidaPorSegundo = 8f; // CA2

    private HealthSystem vida;
    private float ultimoGolpe;
    private float acumulado; // la parte de vida que todavía no llega a 1 punto

    /// <summary>Si ahora mismo está recuperando vida.</summary>
    public bool Regenerando { get; private set; }

    private void Awake()
    {
        vida = GetComponent<HealthSystem>();
        ultimoGolpe = Time.time;
    }

    private void OnEnable() => HealthSystem.Damaged += AlRecibirDanio;
    private void OnDisable() => HealthSystem.Damaged -= AlRecibirDanio;

    // CA3
    private void AlRecibirDanio(HealthSystem quien, int danio, Vector3? desde)
    {
        if (quien != vida) return;
        ultimoGolpe = Time.time;
        acumulado = 0f;
        Regenerando = false;
    }

    private void Update()
    {
        Regenerando = vida.currentHealth > 0 && vida.currentHealth < vida.maxHealth && Time.time - ultimoGolpe >= Espera;
        if (!Regenerando)
        {
            acumulado = 0f;
            return;
        }
        acumulado += VidaPorSegundo * Time.deltaTime;
        int suma = Mathf.FloorToInt(acumulado);
        if (suma <= 0) return;
        acumulado -= suma;
        vida.SetState(Mathf.Min(vida.maxHealth, vida.currentHealth + suma), vida.currentShield); // CA4: el escudo queda igual
    }
}
