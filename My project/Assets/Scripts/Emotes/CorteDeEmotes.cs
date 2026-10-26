using UnityEngine;

// F27 (Emotes), US 262, CA4: mientras el jugador de esta computadora baila (Emotes), revisa cada cuadro si hizo algo que
// corta el baile: moverse, saltar, agacharse, disparar, recargar, cambiar de arma, usar una habilidad, abrir la tienda o
// recibir daño (y morir). Con apretar la tecla alcanza, así lo que hizo pasa en el momento.
public class CorteDeEmotes : MonoBehaviour
{
    private static CorteDeEmotes unico;
    private PlayerMovement jugador;
    private HealthSystem vida;
    private WeaponSwitcher armas;
    private CharacterController cuerpo;
    private int vidaAntes, blindajeAntes;
    private GameObject enManoAntes;

    public static void Asegurar()
    {
        if (unico != null) return;
        var go = new GameObject("Emotes (corte)") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        unico = go.AddComponent<CorteDeEmotes>();
    }

    public static bool Encontrar(out PlayerMovement jugador)
    {
        Camera camara = Camera.main;
        jugador = camara != null ? camara.GetComponentInParent<PlayerMovement>() : null;
        return jugador != null;
    }

    public static bool SePuede(PlayerMovement jugador, bool conPausa)
    {
        if (ShopUI.IsOpen || (PauseMenu.IsPaused && !conPausa)) return false;
        HealthSystem vida = jugador.GetComponent<HealthSystem>();
        return vida == null || vida.currentHealth > 0;
    }

    public static void Empezar(PlayerMovement jugador)
    {
        unico.jugador = jugador;
        unico.vida = jugador.GetComponent<HealthSystem>();
        unico.cuerpo = jugador.GetComponent<CharacterController>();
        unico.armas = jugador.GetComponentInChildren<WeaponSwitcher>();
        unico.vidaAntes = unico.vida != null ? unico.vida.currentHealth : 0;
        unico.blindajeAntes = unico.vida != null ? unico.vida.currentShield : 0;
        unico.enManoAntes = unico.EnMano();
    }

    private GameObject EnMano()
    {
        if (armas == null) return null;
        if (armas.HeldPrimary != null) return armas.HeldPrimary;
        if (armas.HeldSecondary != null) return armas.HeldSecondary;
        if (armas.meleeScript != null && armas.meleeScript.CurrentViewModel != null && armas.meleeScript.CurrentViewModel.activeInHierarchy)
            return armas.meleeScript.CurrentViewModel;
        return null;
    }

    private static readonly GameAction[] Cortan =
    {
        GameAction.Adelante, GameAction.Atras, GameAction.Izquierda, GameAction.Derecha,
        GameAction.Saltar, GameAction.Agacharse, GameAction.Disparar, GameAction.Recargar, GameAction.Habilidad,
        GameAction.ArmaPrincipal, GameAction.ArmaSecundaria, GameAction.Cuchillo, GameAction.Granadas,
        GameAction.Dispositivo, GameAction.Soltar, GameAction.Tienda,
    };

    private void Update()
    {
        if (Emotes.Actual < 0) return;
        if (jugador == null) { Emotes.Cortar(); return; }
        bool corta = ShopUI.IsOpen;
        if (!PauseMenu.IsPaused)
            foreach (GameAction a in Cortan)
                if (KeyBindings.Down(a) || (a <= GameAction.Derecha && KeyBindings.Held(a))) { corta = true; break; }
        if (vida != null)
        {
            // Recibió daño (o murió).
            if (vida.currentHealth <= 0 || vida.currentHealth < vidaAntes || vida.currentShield < blindajeAntes) corta = true;
            vidaAntes = vida.currentHealth;
            blindajeAntes = vida.currentShield;
        }
        if (cuerpo != null && !cuerpo.isGrounded && cuerpo.velocity.y < -2f) corta = true; // se cayó de algo
        if (EnMano() != enManoAntes) corta = true;                                         // cambió de arma
        if (corta) Emotes.Cortar();
    }
}
