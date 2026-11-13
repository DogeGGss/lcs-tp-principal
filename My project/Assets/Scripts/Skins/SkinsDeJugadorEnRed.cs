using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// F35 (Skins de armas), US 380, CA2: en la copia de otro jugador (JugadorEnRed), sus armas con las skins que publicó
// (SkinsEnLasArmas.PropSkins): la de la mano y las de primera persona apagadas (de esas copia el espectador, US 133).
// Se vuelven a pintar cuando las cambia (por ejemplo, al levantar un arma con otra skin, CA4).
public class SkinsDeJugadorEnRed : MonoBehaviourPunCallbacks
{
    private JugadorEnRed jugador;

    public static void Poner(JugadorEnRed jugador)
    {
        if (jugador == null) return;
        SkinsDeJugadorEnRed s = jugador.GetComponent<SkinsDeJugadorEnRed>();
        if (s == null) s = jugador.gameObject.AddComponent<SkinsDeJugadorEnRed>();
        s.jugador = jugador;
        s.Pintar();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
    {
        if (jugador == null || target == null || target != jugador.Dueno || changedProps == null) return;
        if (changedProps.ContainsKey(SkinsEnLasArmas.PropSkins)) Pintar();
    }

    private void Pintar()
    {
        if (jugador == null) return;
        Dictionary<string, CatalogoDeSkins.Skin> skins = SkinsEnLasArmas.De(jugador.Dueno);
        foreach (var (arma, ficha) in jugador.ArmasConFicha())
        {
            CatalogoDeSkins.Arma a = ficha != null ? CatalogoDeSkins.ArmaDeFicha(ficha.name) : null;
            if (arma == null || a == null) continue;
            SkinDeArma.Poner(arma, skins.TryGetValue(a.id, out CatalogoDeSkins.Skin s) ? s : null);
        }
    }
}
