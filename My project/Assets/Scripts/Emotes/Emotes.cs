using UnityEngine;

// F27 (Emotes), US 262: el baile que está haciendo el jugador de esta computadora. Lo empieza la rueda (US 261) o, para
// probar, el muñeco espejo; los demás lo ven porque JugadorEnRed lo manda por la red (y lo baila su copia,
// BaileEnTerceraPersona).
// CA4: se corta solo al moverse, saltar, agacharse, disparar, recargar, cambiar de arma, usar una habilidad, abrir la
// tienda o recibir daño (y al morir): lo revisa CorteDeEmotes.
public static class Emotes
{
    /// <summary>El baile que está haciendo (su número en el catálogo), o -1 si ninguno.</summary>
    public static int Actual { get; private set; } = -1;

    /// <summary>Cambió el baile (empezó o se cortó).</summary>
    public static event System.Action Cambio;

    /// <summary>
    /// Empieza ese baile, si se puede (no muerto ni con la tienda o la pausa abiertas). "conPausa": también con la pausa
    /// abierta (el muñeco espejo: para hacer clic en el Inspector hay que tener la pausa abierta).
    /// </summary>
    public static bool Hacer(int indice, bool conPausa = false)
    {
        if (CatalogoDeEmotes.Actual.De(indice) == null) return false;
        if (!CorteDeEmotes.Encontrar(out PlayerMovement jugador)) return false;
        if (!CorteDeEmotes.SePuede(jugador, conPausa)) return false;
        CorteDeEmotes.Asegurar();
        CorteDeEmotes.Empezar(jugador);
        if (Actual == indice) return true;
        Actual = indice;
        Cambio?.Invoke();
        return true;
    }

    /// <summary>Corta el baile (si estaba bailando).</summary>
    public static void Cortar()
    {
        if (Actual < 0) return;
        Actual = -1;
        Cambio?.Invoke();
    }
}
