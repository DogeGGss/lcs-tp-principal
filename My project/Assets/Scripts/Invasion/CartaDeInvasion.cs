using System.Collections.Generic;
using UnityEngine;

/// <summary>US 293, CA3: la rareza de una carta. Las legendarias salen recién después del primer Jefe (US 303).</summary>
public enum RarezaDeCarta { Comun, Rara, Epica, Legendaria }

// F08 (Modo Invasión), US 293: una carta del puesto de suministros, como datos (nombre, rareza, qué hace y con qué
// armas rinde), para sumar más sin tocar la pantalla de la elección (EleccionDeInvasion).
// PROVISIONAL: por ahora están las de las US 295 y US 296 solo con su texto; cada efecto se programa en su US
// (Aplicar queda vacío hasta entonces). La rareza de cada una es una propuesta para probar.
public class CartaDeInvasion
{
    public string nombre;
    public RarezaDeCarta rareza;
    public string efecto;
    public string rindeCon;
    /// <summary>CA5: de una sola vez (como Logística): una vez elegida no vuelve a salir. Las demás se acumulan hasta
    /// MaximoDeVeces.</summary>
    public bool unaVez;
    /// <summary>Lo que hace al elegirla: el jugador y cuántas veces la tiene ahora (1 a 3). Vacío: todavía no tiene
    /// efecto. Las que actúan después (por ejemplo, al matar) preguntan cuántas tiene con EleccionDeInvasion.Veces.</summary>
    public System.Action<GameObject, int> aplicar;

    /// <summary>CA5: una carta se puede tener como mucho 3 veces (después de la tercera no vuelve a salir).</summary>
    public const int MaximoDeVeces = 3;
    /// <summary>CA5: cuántas veces se puede tener esta (1 si es de una sola vez).</summary>
    public int Maximo => unaVez ? 1 : MaximoDeVeces;

    public static Color ColorDe(RarezaDeCarta rareza) => rareza switch
    {
        RarezaDeCarta.Rara => ShopUIKit.Hex(0x4DA3FF),
        RarezaDeCarta.Epica => ShopUIKit.Hex(0xB06BFF),
        RarezaDeCarta.Legendaria => ShopUIKit.Hex(0xFFC83D),
        _ => ShopUIKit.Hex(0xE8EAED),
    };

    public static string NombreDe(RarezaDeCarta rareza) => rareza switch
    {
        RarezaDeCarta.Rara => "Rara",
        RarezaDeCarta.Epica => "Épica",
        RarezaDeCarta.Legendaria => "Legendaria",
        _ => "Común",
    };

    /// <summary>El color de las opciones que son un arma (US 294).</summary>
    public static readonly Color ColorArma = ShopUIKit.Bad;

    private static List<CartaDeInvasion> todas;

    /// <summary>Todas las cartas que pueden salir en el puesto.</summary>
    public static IReadOnlyList<CartaDeInvasion> Todas => todas ??= Armar();

    private static List<CartaDeInvasion> Armar() => new List<CartaDeInvasion>
    {
        // US 309 a 318 (antes US 295 y 296): las de una sola vez (unaVez) y las que se acumulan hasta 3 veces, según
        // cada historia.
        new CartaDeInvasion { nombre = "Doble cartucho", rareza = RarezaDeCarta.Epica, rindeCon = "Escopeta", unaVez = true,
            efecto = "La escopeta dispara 2 cartuchos juntos, gastando los dos." },
        new CartaDeInvasion { nombre = "Cargador gemelo", rareza = RarezaDeCarta.Rara, rindeCon = "Subfusil", unaVez = true,
            efecto = "La primera vez que se vacía el cargador, se recarga al instante desde la reserva." },
        new CartaDeInvasion { nombre = "Cerrojo suelto", rareza = RarezaDeCarta.Rara, rindeCon = "Francotirador", unaVez = true,
            efecto = "Después de una baja a la cabeza, se puede volver a disparar a los 0,2 s." },
        new CartaDeInvasion { nombre = "Sin intervalo", rareza = RarezaDeCarta.Rara, rindeCon = "Fusil de ráfaga",
            efecto = "Matar saca la pausa entre las próximas 3 ráfagas." },
        new CartaDeInvasion { nombre = "Campo minado", rareza = RarezaDeCarta.Epica, rindeCon = "Gran Capitán",
            efecto = "Las granadas que tocan el piso quedan como minas (hasta 5)." },
        // Las de munición.
        new CartaDeInvasion { nombre = "Logística", rareza = RarezaDeCarta.Rara, rindeCon = "Todas", unaVez = true,
            efecto = "Desde ahora, los puestos recargan toda la munición en vez de la mitad." },
        new CartaDeInvasion { nombre = "Mochila", rareza = RarezaDeCarta.Comun, rindeCon = "Todas",
            efecto = "40 % más de reserva en las dos armas.", aplicar = EfectosDeCartas.Mochila },
        new CartaDeInvasion { nombre = "Reciclador", rareza = RarezaDeCarta.Rara, rindeCon = "Todas",
            efecto = "Cada baja a la cabeza devuelve 5 balas al cargador." },
        new CartaDeInvasion { nombre = "Filo", rareza = RarezaDeCarta.Comun, rindeCon = "Cuchillo", unaVez = true,
            efecto = "Matar con el cuchillo recarga el arma que tenés en la mano." },
        new CartaDeInvasion { nombre = "Carroñero", rareza = RarezaDeCarta.Rara, rindeCon = "Todas",
            efecto = "1 de cada 5 invasores suelta un cargador al morir." },
    };

    /// <summary>CA3: la rareza al azar según la oleada (común, rara y épica). US 303: despuesDelJefe, ya cayó algún
    /// Jefe (CA3: las legendarias, muy raras, se le restan a las comunes); delJefe, la elección del Jefe (CA2: sin
    /// comunes, con más chance de raras, épicas y legendarias).</summary>
    public static RarezaDeCarta RarezaAlAzar(int oleada, bool despuesDelJefe = false, bool delJefe = false)
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        float r = Random.value;
        if (delJefe)
            return r < config.jefeRara ? RarezaDeCarta.Rara
                 : r < config.jefeRara + config.jefeEpica ? RarezaDeCarta.Epica : RarezaDeCarta.Legendaria;
        float comun, rara;
        if (oleada <= 4) { comun = 0.70f; rara = 0.25f; }
        else if (oleada <= 7) { comun = 0.55f; rara = 0.35f; }
        else { comun = 0.40f; rara = 0.40f; }
        float legendaria = despuesDelJefe ? Mathf.Min(config.legendariaNormal, comun) : 0f;
        return r < legendaria ? RarezaDeCarta.Legendaria
             : r < comun ? RarezaDeCarta.Comun
             : r < comun + rara ? RarezaDeCarta.Rara : RarezaDeCarta.Epica;
    }
}
