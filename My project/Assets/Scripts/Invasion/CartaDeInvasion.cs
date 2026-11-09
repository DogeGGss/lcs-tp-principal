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
    /// <summary>Si se acumula y el efecto cambia: el texto cuando sale por segunda y por tercera vez (con lo que da en
    /// total), así se ve que mejora. null: siempre el mismo texto.</summary>
    public string[] efectoRepetida;
    public string rindeCon;
    /// <summary>CA5: de una sola vez (como Logística): una vez elegida no vuelve a salir. Las demás se acumulan hasta
    /// MaximoDeVeces.</summary>
    public bool unaVez;
    /// <summary>Lo que hace al elegirla: el jugador y cuántas veces la tiene ahora (1 a 3). Vacío: todavía no tiene
    /// efecto. Las que actúan después (por ejemplo, al matar) preguntan cuántas tiene con EleccionDeInvasion.Veces.</summary>
    public System.Action<GameObject, int> aplicar;
    /// <summary>Si no es null, la carta sale en la elección solo si da true (por ejemplo, Sin respaldo y Acople no
    /// salen juntas: US 331).</summary>
    public System.Func<bool> puedeSalir;

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

    // US 331: Sin respaldo sale solo con una principal equipada.
    private static bool TienePrincipal()
    {
        WeaponSwitcher armas = Object.FindFirstObjectByType<WeaponSwitcher>();
        return armas != null && armas.PrimaryObj != null;
    }

    private static List<CartaDeInvasion> todas;

    /// <summary>Todas las cartas que pueden salir en el puesto.</summary>
    public static IReadOnlyList<CartaDeInvasion> Todas => todas ??= Armar().FindAll(c => CartasDelSprint5 || System.Array.IndexOf(DelSprint5, c.nombre) < 0);

    // Con false no salen (ni en el puesto ni en los trucos) las cartas que se prueban en el Sprint 5 (F29), para las
    // entregas en las que todavía no entran.
    public static readonly bool CartasDelSprint5 = false; // Entrega del Sprint 4: las del Sprint 5 no entran
    private static readonly string[] DelSprint5 =
    {
        "Cargador gemelo", "Sin intervalo", "Campo minado", "Bala compacta", "Descarga rápida", "Acople", "Tiro de reserva",
        "Disparo concentrado", "Ráfaga creciente", "Mira abierta", "Carga adherente", "Remate", "Bronca", "Sin respaldo",
        "Entrega inmediata", "Alimentación directa",
    };

    private static List<CartaDeInvasion> Armar() => new List<CartaDeInvasion>
    {
        // US 309 a 318 (antes US 295 y 296): las de una sola vez (unaVez) y las que se acumulan hasta 3 veces, según
        // cada historia.
        new CartaDeInvasion { nombre = "Tiro de reserva", rareza = RarezaDeCarta.Rara, rindeCon = "Subfusil y fusil", unaVez = true,
            efecto = "Con el cargador vacío, manteniendo el gatillo sigue disparando desde la reserva, a la mitad de la cadencia.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Disparo concentrado", rareza = RarezaDeCarta.Epica, rindeCon = "Fusil", unaVez = true,
            efecto = "Apuntando, el fusil dispara hasta 5 balas juntas en un solo impacto, cada 0,4 s.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Descarga rápida", rareza = RarezaDeCarta.Rara, rindeCon = "Pistola pesada", unaVez = true,
            efecto = "Disparo cargado: manteniendo el gatillo se carga en 3 etapas y, al soltar, salen 2, 4 o 6 balas seguidas.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Acople", rareza = RarezaDeCarta.Epica, rindeCon = "Pistolas y arma principal", unaVez = true,
            efecto = "Cada 3 tiros de pistola que aciertan, el arma principal dispara sola un tiro hacia la mira.", aplicar = EfectosDeCartas.EnElJuego,
            puedeSalir = () => EleccionDeInvasion.Veces("Sin respaldo") == 0 }, // US 331
        new CartaDeInvasion { nombre = "Sin respaldo", rareza = RarezaDeCarta.Rara, rindeCon = "Cualquier principal", unaVez = true,
            efecto = "Tu arma principal tiene el doble de cargador, pero perdés la secundaria por el resto de la partida.", aplicar = EfectosDeCartas.SinRespaldo,
            puedeSalir = () => EleccionDeInvasion.Veces("Acople") == 0 && TienePrincipal() },
        new CartaDeInvasion { nombre = "Carga adherente", rareza = RarezaDeCarta.Epica, rindeCon = "Gran Capitán", unaVez = true,
            efecto = "Las granadas que le pegan directo a un invasor se le pegan 1 s y explotan con el doble de daño.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Doble cartucho", rareza = RarezaDeCarta.Epica, rindeCon = "Escopeta", unaVez = true,
            efecto = "La escopeta dispara 2 cartuchos juntos, gastando los dos.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Bala compacta", rareza = RarezaDeCarta.Rara, rindeCon = "Escopeta", unaVez = true,
            efecto = "Apuntando, los perdigones salen juntos en un solo proyectil sin dispersión, con el mismo daño.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Cargador gemelo", rareza = RarezaDeCarta.Rara, rindeCon = "Subfusil", unaVez = true,
            efecto = "La primera vez que se vacía el cargador, se recarga al instante desde la reserva.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Cerrojo suelto", rareza = RarezaDeCarta.Rara, rindeCon = "Francotirador", unaVez = true,
            efecto = "Después de una baja a la cabeza, se puede volver a disparar a los 0,2 s.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Mira abierta", rareza = RarezaDeCarta.Rara, rindeCon = "Francotirador", unaVez = true,
            efecto = "Sin apuntar, dispara sin dispersión y al doble de cadencia. Apuntando, dispara normal.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Sin intervalo", rareza = RarezaDeCarta.Rara, rindeCon = "Fusil de ráfaga",
            efecto = "Matar saca la pausa entre las próximas 3 ráfagas.", efectoRepetida = new[] { "Matar saca la pausa entre las próximas 5 ráfagas.", "Matar saca la pausa entre las próximas 7 ráfagas." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Ráfaga creciente", rareza = RarezaDeCarta.Rara, rindeCon = "Fusil de ráfaga",
            efecto = "Cada baja le suma una bala a las ráfagas, hasta 6. Al recargar vuelven a 3.", efectoRepetida = new[] { "Cada baja le suma una bala a las ráfagas, hasta 8. Al recargar vuelven a 3.", "Cada baja le suma una bala a las ráfagas, hasta 10. Al recargar vuelven a 3." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Campo minado", rareza = RarezaDeCarta.Epica, rindeCon = "Gran Capitán",
            efecto = "Las granadas que tocan el piso quedan como minas (hasta 5).", efectoRepetida = new[] { "Las granadas que tocan el piso quedan como minas (hasta 7).", "Las granadas que tocan el piso quedan como minas (hasta 9)." }, aplicar = EfectosDeCartas.EnElJuego },
        // Las de munición.
        new CartaDeInvasion { nombre = "Logística", rareza = RarezaDeCarta.Rara, rindeCon = "Todas", unaVez = true,
            efecto = "Desde ahora, los puestos recargan toda la munición en vez de la mitad.", aplicar = EfectosDeCartas.EnElJuego }, // US 314: EleccionDeInvasion.Abrir
        new CartaDeInvasion { nombre = "Mochila", rareza = RarezaDeCarta.Comun, rindeCon = "Todas",
            efecto = "40 % más de reserva en las dos armas.", efectoRepetida = new[] { "80 % más de reserva en las dos armas (40 % más que ahora).", "120 % más de reserva en las dos armas (40 % más que ahora)." }, aplicar = EfectosDeCartas.Mochila },
        new CartaDeInvasion { nombre = "Reciclador", rareza = RarezaDeCarta.Rara, rindeCon = "Todas",
            efecto = "Cada baja a la cabeza devuelve 5 balas al cargador.", efectoRepetida = new[] { "Cada baja a la cabeza devuelve 10 balas al cargador.", "Cada baja a la cabeza devuelve 15 balas al cargador." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Filo", rareza = RarezaDeCarta.Comun, rindeCon = "Cuchillo", unaVez = true,
            efecto = "Matar con el cuchillo recarga la última arma que tuviste en la mano.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Carroñero", rareza = RarezaDeCarta.Rara, rindeCon = "Todas",
            efecto = "1 de cada 5 invasores suelta un cargador al morir.", efectoRepetida = new[] { "1 de cada 4 invasores suelta un cargador al morir.", "1 de cada 3 invasores suelta un cargador al morir." }, aplicar = EfectosDeCartas.EnElJuego },
        // US 319 a 334 (la segunda tanda): se suman al catálogo a medida que tienen su efecto.
        new CartaDeInvasion { nombre = "Remate", rareza = RarezaDeCarta.Comun, rindeCon = "Cuchillo",
            efecto = "El cuchillo mata de un golpe a los invasores con 25 % de vida o menos (salvo al Jefe).", efectoRepetida = new[] { "El cuchillo mata de un golpe a los invasores con 30 % de vida o menos (salvo al Jefe).", "El cuchillo mata de un golpe a los invasores con 35 % de vida o menos (salvo al Jefe)." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Filo voraz", rareza = RarezaDeCarta.Rara, rindeCon = "Cuchillo",
            efecto = "Cada cuchillazo cura lo mismo que el daño que hace, hasta 25 de vida por golpe.", efectoRepetida = new[] { "Cada cuchillazo cura lo mismo que el daño que hace, hasta 35 de vida por golpe.", "Cada cuchillazo cura lo mismo que el daño que hace, hasta 45 de vida por golpe." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Reserva lista", rareza = RarezaDeCarta.Rara, rindeCon = "Todas", unaVez = true,
            efecto = "El arma guardada se recarga sola desde su reserva después de 2 s.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Bronca", rareza = RarezaDeCarta.Comun, rindeCon = "Todas",
            efecto = "Cuando un invasor te pega, tu próximo disparo suma 5 veces la vida que perdiste.", efectoRepetida = new[] { "Cuando un invasor te pega, tu próximo disparo suma 6 veces la vida que perdiste.", "Cuando un invasor te pega, tu próximo disparo suma 7 veces la vida que perdiste." }, aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Alimentación directa", rareza = RarezaDeCarta.Legendaria, rindeCon = "Todas", unaVez = true,
            efecto = "El cargador y la reserva son un solo depósito: no recargás nunca, hasta que se acaban las balas.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Entrega inmediata", rareza = RarezaDeCarta.Epica, rindeCon = "Todas", unaVez = true,
            efecto = "La elección del puesto se abre apenas se carga, sin esperar a que termine la oleada.", aplicar = EfectosDeCartas.EnElJuego },
        new CartaDeInvasion { nombre = "Despeje", rareza = RarezaDeCarta.Comun, rindeCon = "Todas",
            efecto = "Cuando el puesto se carga, una burbuja empuja 10 m a los invasores del anillo, salvo al Jefe.", efectoRepetida = new[] { "Cuando el puesto se carga, una burbuja empuja 15 m a los invasores del anillo, salvo al Jefe.", "Cuando el puesto se carga, una burbuja empuja 20 m a los invasores del anillo, salvo al Jefe." }, aplicar = EfectosDeCartas.EnElJuego },
    };

    /// <summary>US 334: las cartas que quedan sin uso con Alimentación directa (solo recargan o piden el cargador
    /// vacío); la elección lo avisa.</summary>
    public static readonly string[] SinUsoConAlimentacion = { "Cargador gemelo", "Reserva lista", "Filo", "Tiro de reserva" };
    public const string AlimentacionDirecta = "Alimentación directa";

    /// <summary>US 303 / US 352: en el Sprint 4 no se entrega ninguna legendaria (Alimentación directa es de US 334,
    /// Sprint 5). Mientras esté en false no salen en la elección: las chances de rareza no cambian (CA2 y CA3), pero
    /// cuando toca legendaria sale la carta de la rareza más cercana (una épica). Para el Sprint 5, ponerlo en true.</summary>
    public static bool LegendariasDisponibles = false;

    /// <summary>El texto del efecto para la vez que se va a elegir: si ya se tiene, el de la segunda o la tercera vez.</summary>
    public string EfectoParaLaProxima(int yaLaTiene)
    {
        if (yaLaTiene <= 0 || efectoRepetida == null || efectoRepetida.Length == 0) return efecto;
        return efectoRepetida[Mathf.Min(yaLaTiene, efectoRepetida.Length) - 1];
    }

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
