using System;
using UnityEngine;

// Estadísticas del minijuego de cometas (F21, US 159): los totales de todas las fases de compra y los récords de una sola
// fase. MinijuegoCometas suma cada fase al terminar (CA1) y se guardan con el progreso del jugador (Guardado, CA2), en
// la PC y en la nube (ProgresoNube). Las muestra el Perfil, en Estadísticas (CA3 y CA4).
public static class EstadisticasMinijuego
{
    public const string Clave = "Minijuego.Cometas";

    [Serializable]
    public sealed class Totales
    {
        public int version = 1;
        public int fases, disparos, puntos;
        public int[] aciertos = new int[3]; // por tipo de cometa
        public long reaccionMs;             // suma de los tiempos de reacción de todos los aciertos
        public int recordCometas, recordPuntos;
        public long mejorReaccionMs;        // 0: todavía no le pegó a ninguno

        public int Aciertos => aciertos[0] + aciertos[1] + aciertos[2];
        public float Precision => disparos > 0 ? (float)Aciertos / disparos : 0f;
        public float ReaccionPromedio => Aciertos > 0 ? reaccionMs / 1000f / Aciertos : 0f;
        public bool SinDatos => disparos == 0 && Aciertos == 0;
    }

    public static event Action Cambio;
    private static Totales datos;

    public static Totales Datos => datos ??= Leer(Guardado.GetString(Clave, ""));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        datos = null;
        Cambio = null;
    }

    /// <summary>CA1: al terminar una fase de compra, sus resultados se suman a los totales y a los récords.</summary>
    public static void SumarFase(MinijuegoCometas.Resultados fase)
    {
        if (fase == null || (fase.disparos == 0 && fase.Aciertos == 0)) return;
        Totales t = Datos;
        t.fases++;
        t.disparos += fase.disparos;
        t.puntos += fase.puntos;
        for (int i = 0; i < 3; i++) t.aciertos[i] += fase.aciertos[i];
        t.reaccionMs += (long)Math.Round(fase.reaccionTotal * 1000.0);
        t.recordCometas = Math.Max(t.recordCometas, fase.Aciertos);
        t.recordPuntos = Math.Max(t.recordPuntos, fase.puntos);
        if (fase.Aciertos > 0 && !float.IsInfinity(fase.mejorReaccion))
        {
            long mejor = (long)Math.Round(fase.mejorReaccion * 1000.0);
            if (t.mejorReaccionMs == 0 || mejor < t.mejorReaccionMs) t.mejorReaccionMs = Math.Max(1, mejor);
        }
        Guardar();
        Guardado.Save(); // una vez por fase: se escribe ya (al cerrar el juego, el guardado automático ya pasó)
    }

    /// <summary>Junta lo que vino de la nube: el máximo de cada total y el mejor tiempo.</summary>
    public static void UnirNube(Totales nube)
    {
        if (Unir(Datos, nube)) Guardar();
    }

    /// <summary>Vuelve a leer lo guardado (lo cambió otro juego abierto en esta PC).</summary>
    public static void Recargar()
    {
        datos = null;
        Cambio?.Invoke();
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(Datos));
        Cambio?.Invoke();
    }

    // ---------- Formato y unión (puros, para las pruebas) ----------

    public static Totales Leer(string json)
    {
        Totales t = null;
        if (!string.IsNullOrEmpty(json)) { try { t = JsonUtility.FromJson<Totales>(json); } catch { t = null; } }
        if (t == null) return new Totales();
        if (t.aciertos == null) t.aciertos = new int[3];
        else if (t.aciertos.Length != 3) Array.Resize(ref t.aciertos, 3);
        return t;
    }

    /// <summary>Lo de dos juegos o de la PC y la nube: el máximo de cada total y el menor de los mejores tiempos.</summary>
    public static bool Unir(Totales a, Totales b)
    {
        if (b == null) return false;
        bool cambio = false;
        void Max(ref int x, int y) { if (y > x) { x = y; cambio = true; } }
        void MaxL(ref long x, long y) { if (y > x) { x = y; cambio = true; } }
        Max(ref a.fases, b.fases);
        Max(ref a.disparos, b.disparos);
        Max(ref a.puntos, b.puntos);
        for (int i = 0; i < 3 && i < b.aciertos.Length; i++) Max(ref a.aciertos[i], b.aciertos[i]);
        MaxL(ref a.reaccionMs, b.reaccionMs);
        Max(ref a.recordCometas, b.recordCometas);
        Max(ref a.recordPuntos, b.recordPuntos);
        if (b.mejorReaccionMs > 0 && (a.mejorReaccionMs == 0 || b.mejorReaccionMs < a.mejorReaccionMs)) { a.mejorReaccionMs = b.mejorReaccionMs; cambio = true; }
        return cambio;
    }

    /// <summary>Para Guardado.RegistrarUnion: junta lo de este juego con lo del archivo.</summary>
    public static string Juntar(string mio, string otro)
    {
        Totales a = Leer(mio);
        return Unir(a, Leer(otro)) ? JsonUtility.ToJson(a) : mio;
    }
}
