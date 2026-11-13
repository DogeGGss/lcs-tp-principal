using System.Collections.Generic;
using UnityEngine;

// F31 (Invasión cooperativa), US 289: el personaje se elige una vez adentro de la partida, como en el Deathmatch
// (PersonajesDeathmatch). En Invasión no hay habilidades (US 297): es solo el aspecto, sin restricciones (varios pueden
// usar el mismo).
// - Se abre una sola vez, en el descanso del inicio, antes de la oleada 1. Si el descanso termina sin confirmar, queda el
//   último elegido (el del menú, si no tocó nada).
// - Lo elegido viaja como propiedad del jugador (CooperativoDeInvasion.AnunciarPersonaje) y JugadorEnRed cambia el cuerpo,
//   en el propio jugador y en las copias de los demás.
// - Mientras está abierta no se dispara, ni se usa el cuchillo, ni se tira una granada, ni se cambia de arma (el clic es para
//   elegir); se vuelve a poder cuando se suelta el botón con el que se eligió.
// Lo agrega PartidaEnRed en una partida de Invasión.
public class PersonajesInvasion : MonoBehaviour
{
    private const float SinDescanso = 15f; // s para elegir si la escena no tiene descanso (fase de compra)

    private CharacterSelectScreen pantalla;
    private bool abierta, visto, esperandoSoltar;
    private float abiertaHasta;
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    private void Start() => CooperativoDeInvasion.AnunciarPersonaje(); // hasta que elija, el del menú

    private void OnDestroy()
    {
        if (abierta) Soltar();
        Desbloquear();
        if (pantalla != null) Destroy(pantalla.gameObject);
    }

    private void Update()
    {
        // El clic con el que se eligió no tiene que disparar: las armas vuelven cuando se suelta.
        if (esperandoSoltar && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) Desbloquear();

        OleadasDeInvasion oleadas = PartidaDeInvasion.Actual != null ? PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() : null;
        BuyPhase descanso = BuyPhase.Current;
        bool alInicio = oleadas != null && oleadas.Oleada == 0 && oleadas.Descanso;
        float restante = descanso != null && descanso.IsActive ? descanso.TimeLeft : SinDescanso;

        // Una sola vez, en el descanso del inicio (el que entra o vuelve después sigue con el que tenía).
        if (!visto && !abierta && alInicio && !ShopUI.IsOpen && ResultadoDeInvasion.Actual == null && restante > 1f) Abrir(restante);
        if (!alInicio) visto = true;

        if (abierta)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CameraLook.Quieta = true;
            // Empezó la oleada 1 (o se acabó el tiempo): queda el último elegido.
            if ((!alInicio || Time.time >= abiertaHasta) && pantalla != null) pantalla.ForceTimeUp();
        }
    }

    private void Abrir(float segundos)
    {
        visto = true;
        MatchHud hud = MatchHud.Instance;
        float tiempo = Mathf.Max(1f, segundos - 0.5f);
        pantalla = CharacterSelectScreen.Show(hud != null ? hud.DisplayFont : null, hud != null ? hud.LabelFont : null,
            hud != null ? hud.LabelFont : null, hud != null ? hud.Rounded : null,
            L10n.T("gameplay.ui.invasion_mode") + " · " + DificultadDeInvasion.Nombre, tiempo, AlElegir, null, true,
            "character.no_invasion_abilities");
        if (pantalla == null) return; // no hay personajes
        abierta = true;
        Bloquear();
        abiertaHasta = Time.time + tiempo + 0.5f;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
    }

    private void AlElegir()
    {
        Soltar();
        CooperativoDeInvasion.AnunciarPersonaje();
    }

    private void Soltar()
    {
        abierta = false;
        pantalla = null;
        esperandoSoltar = true; // (Update desbloquea cuando se suelta el botón)
        CameraLook.Quieta = RuedaDeEmotes.Abierta;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        if (ShopUI.IsOpen) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Las armas del jugador de esta computadora (las copias de los demás no disparan solas).
    private void Bloquear()
    {
        Desbloquear();
        PartidaEnRed partida = PartidaEnRed.Actual;
        GameObject jugador = partida != null && partida.Local != null ? partida.Local.gameObject : null;
        if (jugador == null) { PlayerMovement p = FindFirstObjectByType<PlayerMovement>(); if (p != null) jugador = p.gameObject; }
        if (jugador == null) return;
        foreach (Behaviour b in jugador.GetComponentsInChildren<Behaviour>(true))
            if ((b is Pistola || b is Mitre || b is ArmaDeFuego || b is MeleeAttack || b is WeaponSwitcher || b is GrenadeThrower
                 || b is SoltarArmas || b is PlayerAbility) && b.enabled)
            {
                b.enabled = false;
                bloqueados.Add(b);
            }
    }

    private void Desbloquear()
    {
        esperandoSoltar = false;
        foreach (Behaviour b in bloqueados) if (b != null) b.enabled = true;
        bloqueados.Clear();
    }
}
