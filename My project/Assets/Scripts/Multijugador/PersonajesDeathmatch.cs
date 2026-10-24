using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Aspecto del personaje en el Modo Deathmatch. En Deathmatch no hay habilidades (PlayerAbility): el personaje es solo
// el aspecto, por gusto de cada jugador, y lo ven él y los demás.
// - Durante la cuenta regresiva del inicio (PartidaDeathmatch, 10 s) se abre la misma selección de personaje del
//   Táctico, sin bloqueos: varios pueden usar el mismo. La tarjeta no muestra la habilidad. Si se termina la cuenta
//   sin confirmar, queda el último elegido.
// - Lo elegido viaja como propiedad del jugador en la sala ("dm.pj", el nombre del archivo del personaje), así lo ve
//   el que entra o vuelve a la partida. Hasta elegir vale el último personaje elegido en el menú.
// - El cuerpo lo cambia JugadorEnRed (PersonajeDe), en el propio jugador y en las copias de los demás.
// Lo agrega PartidaDeathmatch al empezar.
public class PersonajesDeathmatch : MonoBehaviour
{
    private const string Prop = "dm.pj";

    public static PersonajesDeathmatch Actual { get; private set; }

    private CharacterSelectScreen pantalla;
    private bool abierta, visto;

    public void Iniciar()
    {
        Actual = this;
        Anunciar(CharacterRoster.Selected); // hasta que elija, el del menú
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        if (abierta) Soltar();
        if (pantalla != null) Destroy(pantalla.gameObject);
    }

    /// <summary>El personaje (aspecto) de ese jugador en el Deathmatch, o null si no se sabe.</summary>
    public static CharacterData De(int actor)
    {
        Room sala = PhotonNetwork.CurrentRoom;
        Player jugador = sala != null ? sala.GetPlayer(actor) : null;
        if (jugador == null || !jugador.CustomProperties.TryGetValue(Prop, out object v) || !(v is string nombre)) return null;
        foreach (CharacterData p in CharacterRoster.All) if (p.name == nombre) return p;
        return null;
    }

    private static void Anunciar(CharacterData p)
    {
        if (p == null || !PhotonNetwork.InRoom) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Prop, p.name } });
    }

    private void Update()
    {
        PartidaDeathmatch partida = PartidaDeathmatch.Actual;
        if (partida == null || !partida.Lista) return;
        bool cuenta = partida.FaseActual == PartidaDeathmatch.Fase.Cuenta;

        // Una sola vez, en la cuenta del inicio (el que vuelve en medio del combate sigue con el que tenía).
        if (cuenta && !visto && !abierta && !ShopUI.IsOpen && partida.Restante > 1f) Abrir(partida.Restante);
        if (!cuenta) visto = true;

        if (abierta)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CameraLook.Quieta = true;
            if (!cuenta && pantalla != null) pantalla.ForceTimeUp(); // empezó el combate: queda el último elegido
        }
    }

    private void Abrir(float segundos)
    {
        visto = true;
        MatchHud hud = MatchHud.Instance;
        pantalla = CharacterSelectScreen.Show(hud != null ? hud.DisplayFont : null, hud != null ? hud.LabelFont : null,
            hud != null ? hud.LabelFont : null, hud != null ? hud.Rounded : null,
            "Modo Deathmatch  ·  Solo cambia el aspecto", Mathf.Max(1f, segundos - 0.5f), AlElegir, null, true);
        if (pantalla == null) return; // no hay personajes
        abierta = true;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
    }

    private void AlElegir()
    {
        Soltar();
        Anunciar(CharacterRoster.Selected);
    }

    private void Soltar()
    {
        abierta = false;
        pantalla = null;
        CameraLook.Quieta = RuedaDeEmotes.Abierta;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        if (ShopUI.IsOpen) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
