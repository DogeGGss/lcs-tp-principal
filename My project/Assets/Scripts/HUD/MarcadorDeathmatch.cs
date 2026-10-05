using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Marcador del Deathmatch (US 141), la versión Deathmatch de la US 057 (MatchHud), según la guía de diseño.
// - CA1: en el centro, el tiempo que queda de los 8 minutos; en el último minuto se pone rojo.
// - CA2: a la izquierda, tus bajas sobre 25, con 25 durmientes.
// - CA3: a la derecha, el que va primero y sus bajas; si sos vos, "Vas primero" y cuántas le sacás al segundo.
// - CA4: con Tab, la tabla de todos con bajas y muertes, ordenada por bajas, con tu fila resaltada.
// - CA5: los avisos de bajas son los de la US 057 (JugadorEnRed los manda a todos).
// - CA6: los números salen de la sala (PartidaDeathmatch), así se actualizan para todos en el mismo momento.
// Lo agrega PartidaEnRed al empezar una partida del Modo Deathmatch.
public class MarcadorDeathmatch : MonoBehaviour
{
    private const float UltimoMinuto = 60f;
    private static int Meta => PartidaDeathmatch.BajasParaGanar;

    public void Iniciar()
    {
        MatchHud.TableProvider = Tabla;
    }

    private void OnDestroy()
    {
        MatchHud.TableProvider = null;
        MatchHud.ClearCenter();
    }

    private void Update()
    {
        PartidaDeathmatch dm = PartidaDeathmatch.Actual;
        if (MatchHud.Instance == null || dm == null || !PhotonNetwork.InRoom) return;

        // CA1: el reloj.
        string detalle = $"Gana el primero a {Meta}";
        PartidaDeathmatch.Fase fase = dm.Lista ? dm.FaseActual : PartidaDeathmatch.Fase.Cuenta;
        if (fase == PartidaDeathmatch.Fase.Cuenta) MatchHud.SetCenter("Empieza en", dm.Restante, detalle);
        else if (fase == PartidaDeathmatch.Fase.Terminada) MatchHud.SetCenter("Fin de la partida", null, detalle);
        else if (dm.Restante <= UltimoMinuto) MatchHud.SetCenter("Último minuto", dm.Restante, detalle, true, null, true);
        else MatchHud.SetCenter("Deathmatch", dm.Restante, detalle);

        // CA2: tus bajas.
        List<Player> puestos = PartidaDeathmatch.Puestos();
        Player yo = PhotonNetwork.LocalPlayer;
        int mias = PartidaDeathmatch.Bajas(yo.ActorNumber);
        MatchHud.SetSide(true, $"{mias}{Sobre()}", "Tus bajas", ShopUIKit.Ink, Meta, mias, true);

        // CA3: el que va primero.
        Player lider = puestos.Count > 0 ? puestos[0] : null;
        Player otro = null;
        foreach (Player p in puestos) if (!p.IsLocal) { otro = p; break; }
        if (lider != null && lider.IsLocal && mias > 0)
        {
            int ventaja = otro != null ? mias - PartidaDeathmatch.Bajas(otro.ActorNumber) : mias;
            MatchHud.SetSide(false, $"+{ventaja}", otro != null ? $"Vas primero  <color=#8E96A3>sobre {otro.NickName}</color>" : "Vas primero",
                ShopUIKit.Ink, Meta, mias, true);
        }
        else if (lider != null && !lider.IsLocal && PartidaDeathmatch.Bajas(lider.ActorNumber) > 0)
        {
            int suyas = PartidaDeathmatch.Bajas(lider.ActorNumber);
            MatchHud.SetSide(false, $"{suyas}{Sobre()}", $"Va primero  <color=#F3F4F6>{lider.NickName}</color>", MatchHud.RivalColor, Meta, suyas, true);
        }
        else MatchHud.SetSide(false, "—", "Va primero", ShopUIKit.Mute, Meta, 0, true);
    }

    private static string Sobre() => $"<size=55%><color=#8E96A3> / {Meta}</color></size>";

    // CA4: la tabla con Tab.
    private static MatchHud.Table Tabla()
    {
        List<Player> puestos = PartidaDeathmatch.Puestos();
        var tabla = new MatchHud.Table
        {
            corner = $"Deathmatch  ·  Gana el primero a {Meta} bajas",
            columns = new[] { "Jugador", "Bajas", "Muertes", "Diferencia" },
            widths = new[] { 470f, 110f, 110f, 110f }
        };
        var seccion = new MatchHud.Section();
        for (int i = 0; i < puestos.Count; i++)
        {
            Player p = puestos[i];
            int b = PartidaDeathmatch.Bajas(p.ActorNumber), m = PartidaDeathmatch.Muertes(p.ActorNumber), dif = b - m;
            string nombre = $"<color=#8E96A3>{i + 1}.</color>  {p.NickName}" + (p.IsLocal ? " <color=#8E96A3>(vos)</color>" : "");
            seccion.rows.Add(new MatchHud.Row
            {
                cells = new[] { nombre, b.ToString(), m.ToString(), dif > 0 ? $"+{dif}" : dif.ToString() },
                highlight = p.IsLocal
            });
        }
        tabla.sections.Add(seccion);
        return tabla;
    }
}
