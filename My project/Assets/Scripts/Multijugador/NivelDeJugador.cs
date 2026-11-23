using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// US 278 (F28): el nivel del jugador, un Elo simple que sube al ganar y baja al perder, y que usan los equipos parejos de
// buscar partida (US 280).
// - CA1: arranca en 1000.
// - CA2: al ver el resultado de una partida de buscar partida, nivel nuevo = nivel + 32 × (resultado − esperado),
//   redondeado y nunca menos de 100, con esperado = 1 ÷ (1 + 10 ^ ((rival − nivel) ÷ 400)). Táctico: el rival es el
//   promedio del equipo contrario y el resultado, 1 si ganó y 0 si perdió. Deathmatch: el rival es el promedio de los
//   demás y el resultado, 1 en la mitad de arriba de la tabla, 0 en la de abajo y 0,5 en el puesto del medio.
//   Se usan los niveles que publicó cada uno al entrar a la sala (los de cuando empezó la partida).
// - CA3: las salas por código, la Invasión y el campo de práctica no lo cambian; abandonar tampoco (no hay resultado).
// - CA4: se ve en el Perfil. CA5: se publica al entrar a una sala. CA6: se guarda con la cuenta y en la nube.
public static class NivelDeJugador
{
    public const string Clave = "Nivel.Jugador";
    public const string Prop = "nivel";
    public const int Inicial = 1000, Minimo = 100, K = 32;

    [Serializable] private class Datos { public int nivel = Inicial; public int partidas; }

    private static Datos d;
    private static Datos D
    {
        get
        {
            if (d != null) return d;
            string json = Guardado.GetString(Clave, "");
            if (!string.IsNullOrEmpty(json)) { try { d = JsonUtility.FromJson<Datos>(json); } catch { d = null; } }
            return d ??= new Datos();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSesion() => d = null;

    public static void Recargar() => d = null;

    public static int Valor => D.nivel;
    /// <summary>Las partidas de buscar partida que contaron para el nivel (para juntarlo con el de la nube).</summary>
    public static int Partidas => D.partidas;

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(D));
        Guardado.Save();
    }

    // ---------- Regla (función pura: la prueban los ejemplos de la US) ----------

    public static float Esperado(int nivel, float rival) => 1f / (1f + Mathf.Pow(10f, (rival - nivel) / 400f));

    public static int Nuevo(int nivel, float rival, float resultado) =>
        Mathf.Max(Minimo, nivel + Mathf.RoundToInt(K * (resultado - Esperado(nivel, rival))));

    // ---------- En la sala ----------

    /// <summary>CA5: publica el nivel al entrar a una sala (Multijugador.OnJoinedRoom).</summary>
    public static void Publicar()
    {
        if (PhotonNetwork.LocalPlayer != null) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Prop, Valor } });
    }

    /// <summary>El nivel que publicó un jugador; si no publicó (una versión vieja), 1000 (US 280, CA3).</summary>
    public static int De(Player p) => p != null && p.CustomProperties.TryGetValue(Prop, out object v) && v is int n ? n : Inicial;

    /// <summary>
    /// CA2 y CA3: al ver el resultado. Solo cuenta en las partidas de buscar partida. Devuelve el cambio (0 si no contó)
    /// o null si no correspondía.
    /// </summary>
    public static int? AlTerminarTactico(bool gano)
    {
        if (!Multijugador.Existe || !Multijugador.Instancia.EnPartidaPublica) return null;
        int mio = EquiposTacticos.Local;
        var rivales = new List<int>();
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!p.IsLocal && EquiposTacticos.DeActor(p.ActorNumber) != mio) rivales.Add(De(p));
        return Aplicar(Promedio(rivales), gano ? 1f : 0f);
    }

    public static int? AlTerminarDeathmatch()
    {
        if (!Multijugador.Existe || !Multijugador.Instancia.EnPartidaPublica) return null;
        List<Player> puestos = PartidaDeathmatch.Puestos();
        int n = puestos.Count, yo = puestos.FindIndex(p => p.IsLocal);
        if (yo < 0 || n < 2) return null;
        var otros = new List<int>();
        foreach (Player p in puestos) if (!p.IsLocal) otros.Add(De(p));
        return Aplicar(Promedio(otros), ResultadoPorPuesto(yo, n));
    }

    /// <summary>Deathmatch: 1 en la mitad de arriba, 0 en la de abajo y 0,5 en el puesto del medio (cantidad impar).</summary>
    public static float ResultadoPorPuesto(int puesto, int cantidad)
    {
        if (cantidad % 2 == 1 && puesto == cantidad / 2) return 0.5f;
        return puesto < cantidad / 2f ? 1f : 0f;
    }

    private static float Promedio(List<int> niveles)
    {
        if (niveles.Count == 0) return Inicial;
        float s = 0f;
        foreach (int x in niveles) s += x;
        return s / niveles.Count;
    }

    private static int Aplicar(float rival, float resultado)
    {
        int antes = D.nivel;
        D.nivel = Nuevo(antes, rival, resultado);
        D.partidas++;
        Guardar();
        return D.nivel - antes;
    }

    // ---------- Nube (ProgresoNube) ----------

    /// <summary>El de la nube manda si contó más partidas que el de esta PC (el nivel sube y baja: no se junta con el máximo).</summary>
    public static void FusionarNube(int nivel, int partidas)
    {
        if (partidas <= D.partidas || nivel <= 0) return;
        D.nivel = Mathf.Max(Minimo, nivel);
        D.partidas = partidas;
        Guardar();
    }
}
