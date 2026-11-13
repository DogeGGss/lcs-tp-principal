using System.Collections;
using System.Collections.Generic;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// F31 (Invasión cooperativa), US 359: volver a la partida con lo que se tenía. La agrega PartidaDeInvasion en
// cooperativo. El volver en sí (los 2 minutos, "Volver a la partida" en el menú y los avisos de "se desconectó" y
// "volvió a la partida") es el de la US 195 (Reconexion, Multijugador, PartidaEnRed); acá va lo propio de Invasión:
// - CA2: mientras se juega, cada uno publica como propiedades suyas en la sala sus puntos (la billetera), sus cartas
//   (cuáles y cuántas veces, en orden) y la munición de sus dos armas. El equipo ya lo publica la tienda (ShopUI) y las
//   bajas y los puntos ganados, CompanerosEnInvasion. Photon guarda las propiedades del que se desconecta mientras
//   tiene su lugar: al volver se recupera todo eso.
// - CA2: vuelve como uno que recién entra (US 358, CA2): durante una oleada mira a sus compañeros y aparece en la
//   siguiente (CaidaEnInvasion).
public class ReconexionEnInvasion : MonoBehaviour
{
    private const string PropBilletera = "inv.pl", PropCartas = "inv.cartas", PropMunicion = "inv.mun";
    private const float CadaPublicar = 1f;

    private float proximo;
    private string cartasPublicadas;
    private int billeteraPublicada = int.MinValue;
    private int[] municionPublicada;
    private bool recuperado;

    private void Update()
    {
        if (!CooperativoDeInvasion.Activo) return;
        PartidaEnRed partida = PartidaEnRed.Actual;
        if (partida == null || !partida.Lista || partida.Local == null) return;
        if (!recuperado)
        {
            recuperado = true;
            if (Reconexion.Volvio)
            {
                // Que no publique mientras recupera: publicaría las cartas y la munición de recién entrado (ninguna) y
                // pisaría las guardadas antes de leerlas. Recuperar lo vuelve a habilitar al terminar.
                proximo = float.MaxValue;
                StartCoroutine(Recuperar(partida.Local.gameObject));
            }
            else proximo = Time.unscaledTime + 2f; // (que no publique antes de tener el equipo inicial)
            return;
        }
        if (Time.unscaledTime < proximo) return;
        proximo = Time.unscaledTime + CadaPublicar;
        Publicar(partida.Local.gameObject);
    }

    // ---------- Publicar ----------

    private void Publicar(GameObject jugador)
    {
        var cambios = new Hashtable();
        PlayerWallet billetera = jugador.GetComponent<PlayerWallet>();
        if (billetera != null && billetera.Money != billeteraPublicada)
        {
            billeteraPublicada = billetera.Money;
            cambios[PropBilletera] = billeteraPublicada;
        }
        string cartas = Cartas();
        if (cartas != cartasPublicadas)
        {
            cartasPublicadas = cartas;
            cambios[PropCartas] = cartas;
        }
        int[] municion = Municion(Armas(jugador));
        if (municion != null && (municionPublicada == null || !Iguales(municion, municionPublicada)))
        {
            municionPublicada = municion;
            cambios[PropMunicion] = municion;
        }
        if (cambios.Count > 0) PhotonNetwork.LocalPlayer.SetCustomProperties(cambios);
    }

    // "Nombre:veces|Nombre:veces", en el orden en que se eligieron.
    private static string Cartas()
    {
        var sb = new StringBuilder();
        foreach (CartaDeInvasion c in EleccionDeInvasion.Tenidas)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(c.nombre).Append(':').Append(EleccionDeInvasion.Veces(c));
        }
        return sb.ToString();
    }

    private static WeaponSwitcher Armas(GameObject jugador) => jugador != null ? jugador.GetComponentInChildren<WeaponSwitcher>(true) : null;

    // La principal y la secundaria: cargador y reserva de cada una (-1: no tiene).
    private static int[] Municion(WeaponSwitcher armas)
    {
        if (armas == null) return null;
        var m = new int[4];
        Leer(armas.PrimaryObj, out m[0], out m[1]);
        Leer(armas.SecondaryObj, out m[2], out m[3]);
        return m;
    }

    private static void Leer(GameObject arma, out int cargador, out int reserva)
    {
        cargador = reserva = -1;
        if (arma == null) return;
        if (arma.TryGetComponent(out ArmaDeFuego fuego)) { cargador = fuego.currentAmmo; reserva = fuego.reserveAmmo; }
        else if (arma.TryGetComponent(out Mitre mitre)) { cargador = mitre.currentAmmo; reserva = mitre.reserveAmmo; }
        else if (arma.TryGetComponent(out Pistola pistola)) { cargador = pistola.currentAmmo; reserva = pistola.reserveAmmo; }
    }

    private static void Poner(GameObject arma, int cargador, int reserva)
    {
        if (arma == null || cargador < 0) return;
        if (arma.TryGetComponent(out ArmaDeFuego fuego)) { fuego.currentAmmo = cargador; fuego.reserveAmmo = Mathf.Max(0, reserva); }
        else if (arma.TryGetComponent(out Mitre mitre)) { mitre.currentAmmo = cargador; mitre.reserveAmmo = Mathf.Max(0, reserva); }
        else if (arma.TryGetComponent(out Pistola pistola)) { pistola.currentAmmo = cargador; pistola.reserveAmmo = Mathf.Max(0, reserva); }
    }

    private static bool Iguales(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // ---------- CA2: recuperar ----------

    private IEnumerator Recuperar(GameObject jugador)
    {
        Player yo = PhotonNetwork.LocalPlayer;
        // Una copia: las propiedades del jugador cambian mientras esto espera (y no se tienen que perder).
        var props = new Hashtable();
        foreach (var par in yo.CustomProperties) props[par.Key] = par.Value;
        Debug.Log("[Invasión] Volvió a la partida: recupera sus puntos, su equipo, sus cartas y su munición.");

        // Los puntos (la billetera) y lo ganado (bajas y puntos, para el resultado y la tabla).
        PlayerWallet billetera = jugador.GetComponent<PlayerWallet>();
        if (billetera != null && props.TryGetValue(PropBilletera, out object pl) && pl is int monto) billetera.Set(monto);
        PuntosDeInvasion puntos = FindFirstObjectByType<PuntosDeInvasion>();
        if (puntos != null) puntos.Recuperar(Numero(props, CompanerosEnInvasion.PropBajas), Numero(props, CompanerosEnInvasion.PropPuntos));

        // El equipo (lo que publicó la tienda).
        PlayerLoadout equipo = jugador.GetComponent<PlayerLoadout>();
        if (equipo != null) equipo.Restaurar(Numero(props, ShopUI.PropPrincipal, -1), Numero(props, ShopUI.PropSecundaria, -1));
        yield return null;
        yield return null;

        // Las cartas, en el orden en que las tenía (cada una con lo que hace: Dar las aplica).
        float hasta = Time.unscaledTime + 5f;
        while (EleccionDeInvasion.Actual == null && Time.unscaledTime < hasta) yield return null;
        if (EleccionDeInvasion.Actual != null && props.TryGetValue(PropCartas, out object c) && c is string cartas && cartas.Length > 0)
        {
            var porNombre = new Dictionary<string, CartaDeInvasion>();
            foreach (CartaDeInvasion carta in CartaDeInvasion.Todas) porNombre[carta.nombre] = carta;
            foreach (string parte in cartas.Split('|'))
            {
                int dos = parte.LastIndexOf(':');
                if (dos <= 0 || !int.TryParse(parte.Substring(dos + 1), out int veces)) continue;
                if (!porNombre.TryGetValue(parte.Substring(0, dos), out CartaDeInvasion carta)) continue;
                for (int i = EleccionDeInvasion.Veces(carta); i < veces; i++) EleccionDeInvasion.Actual.Dar(carta);
            }
        }
        yield return null;

        // La munición de las dos armas (después de las cartas: Mochila y Sin respaldo cambian los cargadores).
        if (props.TryGetValue(PropMunicion, out object m) && m is int[] municion && municion.Length >= 4)
        {
            WeaponSwitcher armas = Armas(jugador);
            if (armas != null)
            {
                Poner(armas.PrimaryObj, municion[0], municion[1]);
                Poner(armas.SecondaryObj, municion[2], municion[3]);
            }
        }
        proximo = Time.unscaledTime + 1f;
    }

    private static int Numero(Hashtable props, string clave, int siNo = 0) => props.TryGetValue(clave, out object v) && v is int n ? n : siNo;
}
