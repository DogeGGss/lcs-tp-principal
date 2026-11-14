using System.Collections.Generic;
using System.Text;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// F35 (Skins de armas), US 380: las skins en las armas del jugador de esta computadora (WeaponSwitcher lo agrega solo).
// - CA1: en primera persona, en todos los modos y en el campo de práctica, cada arma con la skin equipada (SkinsDeArmas).
// - CA2: en multijugador la publica (PropSkins) para que los demás la vean en su mano (SkinsDeJugadorEnRed).
// - CA3: al comprar, elegir o cambiar de arma ya está puesta: las armas de primera persona están todas desde el
//   principio y se pintan juntas.
// - CA4: un arma soltada lleva la skin de su dueño (ArmaEnPiso); el que la levanta la usa con esa skin hasta que la
//   pierde (la suelta, muere o la cambia) y los demás también la ven así.
public class SkinsEnLasArmas : MonoBehaviour
{
    /// <summary>Propiedad del jugador en la sala: "arma=skin;arma=skin" (skin vacía: sin skin).</summary>
    public const string PropSkins = "skn";

    public static SkinsEnLasArmas Local { get; private set; }

    private WeaponSwitcher switcher;
    private PlayerLoadout loadout;
    // CA4: las armas levantadas del piso, con la skin de quien la soltó ("": sin skin), por arma.
    private readonly Dictionary<string, string> levantadas = new Dictionary<string, string>();
    private string publicado;

    private void Awake()
    {
        switcher = GetComponentInParent<WeaponSwitcher>();
        loadout = GetComponentInParent<PlayerLoadout>();
    }

    private void Start()
    {
        Local = this;
        SkinsDeArmas.Cambio += Aplicar;
        if (loadout != null) loadout.Changed += AlCambiarEquipo;
        Aplicar();
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
        SkinsDeArmas.Cambio -= Aplicar;
        if (loadout != null) loadout.Changed -= AlCambiarEquipo;
    }

    /// <summary>El id de la skin que lleva el arma de esa ficha ("" o null: sin skin).</summary>
    public string SkinDe(ShopItem ficha)
    {
        CatalogoDeSkins.Arma arma = ficha != null ? CatalogoDeSkins.ArmaDeFicha(ficha.name) : null;
        if (arma == null) return null;
        if (levantadas.TryGetValue(arma.id, out string skin)) return skin;
        return SkinsDeArmas.EquipadaDe(arma.id)?.id;
    }

    /// <summary>US 381: los efectos de disparo de la skin del arma de esa ficha, si es legendaria (si no, null). Se mira
    /// la skin que tiene puesta el arma (lo que se ve: también la que se le puso desde Skins de prueba); si no, la que le
    /// toca.</summary>
    public static CatalogoDeSkins.Efectos EfectosDe(ShopItem ficha)
    {
        if (Local == null) return null;
        GameObject arma = ArmaDe(ficha);
        SkinDeArma puesta = arma != null ? arma.GetComponent<SkinDeArma>() : null;
        if (puesta != null && puesta.Puesta != null) return EfectosDeSkin.De(puesta.Puesta);
        return EfectosDeSkin.De(CatalogoDeSkins.De(Local.SkinDe(ficha)));
    }

    /// <summary>US 381: la ficha de un arma de primera persona del jugador (para Skins de prueba), o null.</summary>
    public static ShopItem FichaDeArma(GameObject arma) => Local != null ? Local.FichaDe(arma) : null;

    /// <summary>US 381: el arma de primera persona de esa ficha (para su calor al disparar), o null.</summary>
    public static GameObject ArmaDe(ShopItem ficha)
    {
        if (Local == null || Local.switcher == null || ficha == null) return null;
        foreach (GameObject arma in Local.switcher.Principales()) if (Local.FichaDe(arma) == ficha) return arma;
        foreach (GameObject arma in Local.switcher.Secundarias()) if (Local.FichaDe(arma) == ficha) return arma;
        return null;
    }

    /// <summary>CA4: levantó un arma del piso con esa skin (la de quien la soltó).</summary>
    public void Levantada(ShopItem ficha, string skin)
    {
        CatalogoDeSkins.Arma arma = ficha != null ? CatalogoDeSkins.ArmaDeFicha(ficha.name) : null;
        if (arma == null) return;
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De(skin);
        levantadas[arma.id] = s != null && s.arma == arma ? s.id : "";
        Aplicar();
    }

    // La levantada deja de contar cuando ya no la tiene (la soltó, murió o compró otra en ese espacio).
    private void AlCambiarEquipo()
    {
        if (levantadas.Count == 0) return;
        var quitar = new List<string>();
        foreach (string arma in levantadas.Keys)
            if (!Tiene(arma)) quitar.Add(arma);
        if (quitar.Count == 0) return;
        foreach (string arma in quitar) levantadas.Remove(arma);
        Aplicar();
    }

    private bool Tiene(string arma)
    {
        if (loadout == null) return false;
        return (loadout.Primary != null && CatalogoDeSkins.ArmaDeFicha(loadout.Primary.name)?.id == arma) ||
               (loadout.Secondary != null && CatalogoDeSkins.ArmaDeFicha(loadout.Secondary.name)?.id == arma);
    }

    /// <summary>Pinta todas las armas de primera persona y publica las skins en la sala.</summary>
    public void Aplicar()
    {
        if (switcher != null)
        {
            foreach (GameObject arma in switcher.Principales()) Pintar(arma);
            foreach (GameObject arma in switcher.Secundarias()) Pintar(arma);
        }
        Publicar();
    }

    private void Pintar(GameObject arma)
    {
        ShopItem ficha = FichaDe(arma);
        if (ficha == null || CatalogoDeSkins.ArmaDeFicha(ficha.name) == null) return;
        SkinDeArma.Poner(arma, CatalogoDeSkins.De(SkinDe(ficha)));
    }

    // La pistola inicial puede no tener la ficha cargada: es la secundaria inicial de la tienda (como en SoltarArmas).
    private ShopItem FichaDe(GameObject arma)
    {
        ShopItem item = WeaponSwitcher.FichaDe(arma);
        // El Mitre y la pistola pueden no tener la ficha asignada: la buscan en el catálogo (la que usan al disparar).
        if (item == null && arma != null)
        {
            Mitre mitre = arma.GetComponent<Mitre>();
            Pistola pistola = arma.GetComponent<Pistola>();
            item = mitre != null ? mitre.Ficha : pistola != null ? pistola.Ficha : null;
        }
        if (item == null && switcher != null && arma == switcher.pistolObj && loadout != null && loadout.Catalog != null)
            item = loadout.Catalog.starterSecondary;
        return item;
    }

    // CA2: "mitre=mitre-rift;urquiza=;..." para los demás jugadores.
    private void Publicar()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        var texto = new StringBuilder();
        foreach (CatalogoDeSkins.Arma arma in CatalogoDeSkins.Armas)
        {
            string skin = levantadas.TryGetValue(arma.id, out string l) ? l : SkinsDeArmas.EquipadaDe(arma.id)?.id;
            if (string.IsNullOrEmpty(skin) && !levantadas.ContainsKey(arma.id)) continue;
            if (texto.Length > 0) texto.Append(';');
            texto.Append(arma.id).Append('=').Append(skin ?? "");
        }
        string valor = texto.ToString();
        if (valor == publicado) return;
        publicado = valor;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropSkins, valor } });
    }

    /// <summary>Las skins que publicó ese jugador: arma → skin (sin las armas sin skin).</summary>
    public static Dictionary<string, CatalogoDeSkins.Skin> De(Photon.Realtime.Player jugador)
    {
        var skins = new Dictionary<string, CatalogoDeSkins.Skin>();
        if (jugador == null || !jugador.CustomProperties.TryGetValue(PropSkins, out object v) || !(v is string texto)) return skins;
        foreach (string par in texto.Split(';'))
        {
            int igual = par.IndexOf('=');
            if (igual <= 0) continue;
            CatalogoDeSkins.Skin s = CatalogoDeSkins.De(par.Substring(igual + 1));
            if (s != null && s.arma.id == par.Substring(0, igual)) skins[s.arma.id] = s;
        }
        return skins;
    }
}
