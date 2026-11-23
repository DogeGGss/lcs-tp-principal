using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// F32 (US 362 y US 365): junta lo que hizo el jugador en la partida para los desafíos de la temporada y, al terminar
// (lo llaman las pantallas de resultado), suma la experiencia y el avance al pase. Una partida abandonada no llega al
// resultado y no suma nada: lo juntado se descarta al cargar otra escena.
// - Bajas a otros jugadores (JugadorEnRed): a la cabeza, con cada arma y sin apuntar con el Último Tren.
// - Invasión: alfas y Jefe eliminados por el jugador (Npc.Murio) y puestos de suministros cargados.
public static class DesafiosDePartida
{
    private static readonly Dictionary<string, int> avances = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        // Sin recarga de dominio, los eventos estáticos siguen de la sesión anterior: primero se sacan.
        Npc.Murio -= AlMorirNpc;
        Npc.Murio += AlMorirNpc;
        PuestoDeSuministros.SeCargo -= AlCargarPuesto;
        PuestoDeSuministros.SeCargo += AlCargarPuesto;
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        avances.Clear();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (modo == LoadSceneMode.Single) avances.Clear();
    }

    private static void Sumar(string id, int cuanto = 1)
    {
        avances.TryGetValue(id, out int n);
        avances[id] = n + cuanto;
    }

    // ---------- Durante la partida ----------

    /// <summary>El jugador local eliminó a otro jugador (Táctico o Deathmatch). arma: el nombre del aviso de baja.</summary>
    public static void BajaPvP(string arma, bool cabeza)
    {
        if (cabeza) Sumar("cabezas");
        CatalogoDeSkins.Arma a = ArmaDe(arma);
        if (a == null) return;
        Sumar("arma." + a.id);
        // Sin apuntar: la mira no estaba puesta al disparar (al disparar se sale de la mira durante el cerrojo, así que
        // cuenta si se sacó hace menos de un segundo y medio).
        bool conMira = MiraTelescopica.Puesta || Time.unscaledTime - MiraTelescopica.UltimaVezPuesta < 1.5f;
        if (a.id == "ultimo-tren" && !conMira) Sumar("sin-mira");
    }

    private static CatalogoDeSkins.Arma ArmaDe(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        string n = nombre.Trim().ToLowerInvariant();
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            if (a.nombre.ToLowerInvariant() == n || (!string.IsNullOrEmpty(a.ficha) && a.ficha.ToLowerInvariant() == n)) return a;
        if (n == "pistola") return CatalogoDeSkins.ArmaDe("la-portena"); // la pistola de entrada
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            if (n.Contains(a.nombre.ToLowerInvariant())) return a;
        return null;
    }

    private static void AlMorirNpc(Npc npc, int puntos, bool cabeza)
    {
        if (npc == null || npc.LoMatoOtro) return; // en cooperativo, la baja es del que lo mató
        if (npc.Alfa != null) Sumar("invasion.alfas");
        if (npc.Tipo != null && npc.Tipo.esJefe) Sumar("invasion.jefe");
    }

    private static void AlCargarPuesto(PuestoDeSuministros puesto) => Sumar("invasion.puestos");

    // ---------- Al terminar ----------

    /// <summary>Táctico o Deathmatch: 100 de experiencia, y 100 más si ganó.</summary>
    public static PaseDeBatalla.Resumen TerminarMultijugador(bool deathmatch, bool gano)
    {
        if (gano) Sumar(deathmatch ? "ganar.deathmatch" : "ganar.tactico");
        if (!deathmatch)
        {
            CharacterData personaje = CharacterRoster.Selected;
            if (personaje != null) Sumar("personaje." + PaseDeBatalla.IdDePersonaje(personaje.displayName));
        }
        return Terminar(PaseDeBatalla.XpPartida + (gano ? PaseDeBatalla.XpVictoria : 0), null);
    }

    /// <summary>Invasión: 10 de experiencia por oleada superada. bajas: los enemigos que eliminó el jugador.</summary>
    public static PaseDeBatalla.Resumen TerminarInvasion(int oleadaAlcanzada, int oleadasSuperadas, int bajas)
    {
        if (bajas > 0) Sumar("invasion.bajas", bajas);
        return Terminar(Mathf.Max(0, oleadasSuperadas) * PaseDeBatalla.XpPorOleada,
            new Dictionary<string, int> { { "invasion.oleada", oleadaAlcanzada } });
    }

    private static PaseDeBatalla.Resumen Terminar(int xp, Dictionary<string, int> maximos)
    {
        PaseDeBatalla.Resumen r = PaseDeBatalla.Sumar(xp, new Dictionary<string, int>(avances), maximos);
        avances.Clear();
        return r;
    }
}
