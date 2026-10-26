using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Opiniones de "Danos tu opinión" (VentanaFeedback) que todavía no llegaron a la base. Quedan en un archivo de la PC
// hasta que LogrosOnline las manda (rw_send_feedback, Backend/Supabase/opiniones.sql): sin conexión se envían cuando
// vuelve, y reintentar no las duplica porque cada una lleva su id. La base avisa a Discord de cada opinión nueva.
public static class Opiniones
{
    public const int Maximo = 500;
    /// <summary>Los tipos que acepta la base, en el orden de los botones de la ventana.</summary>
    public static readonly string[] Tipos = { "opinion", "sugerencia", "error" };

    [Serializable] public sealed class Opinion { public string id, tipo, texto, version, fecha; public int valoracion; }
    [Serializable] private sealed class Archivo { public List<Opinion> pendientes = new List<Opinion>(); }

    private static Archivo datos;
    private static string Ruta => Path.Combine(Application.persistentDataPath, "opiniones-pendientes.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => datos = null;

    private static Archivo Datos
    {
        get
        {
            if (datos != null) return datos;
            try { if (File.Exists(Ruta)) datos = JsonUtility.FromJson<Archivo>(File.ReadAllText(Ruta)); } catch { }
            datos ??= new Archivo();
            datos.pendientes ??= new List<Opinion>();
            return datos;
        }
    }

    public static int Pendientes => Datos.pendientes.Count;
    /// <summary>La más vieja que falta enviar, o null.</summary>
    public static Opinion Primera => Datos.pendientes.Count > 0 ? Datos.pendientes[0] : null;

    /// <summary>Guarda la opinión para enviarla. tipo es el índice de Tipos; valoracion, de 0 (sin valorar) a 5.</summary>
    public static Opinion Encolar(int tipo, int valoracion, string texto)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length > Maximo) texto = texto.Substring(0, Maximo);
        var o = new Opinion
        {
            id = Guid.NewGuid().ToString(), tipo = Tipos[Mathf.Clamp(tipo, 0, Tipos.Length - 1)],
            valoracion = Mathf.Clamp(valoracion, 0, 5), texto = texto, version = Application.version,
            fecha = DateTime.UtcNow.ToString("O"),
        };
        Datos.pendientes.Add(o);
        Guardar();
        return o;
    }

    /// <summary>Ya llegó a la base (o la base la rechazó para siempre): se saca de la cola.</summary>
    public static void Quitar(string id)
    {
        if (Datos.pendientes.RemoveAll(o => o.id == id) > 0) Guardar();
    }

    private static void Guardar()
    {
        try { Guardado.EscribirAtomico(Ruta, JsonUtility.ToJson(Datos, true)); }
        catch (Exception ex) { Debug.LogWarning("Opiniones: no se pudo guardar la cola. " + ex.Message); }
    }
}
