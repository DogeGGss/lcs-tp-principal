using UnityEngine;

// Lo que hace una habilidad (F04). PlayerAbility lo agrega solo al jugador según AbilityData.efecto, y al apretar la
// tecla le pregunta si se puede usar y lo aplica. Si Aplicar devuelve false (no se pudo), la carga no se gasta.
public abstract class EfectoHabilidad : MonoBehaviour
{
    public AbilityData Habilidad { get; set; }

    /// <summary>El componente de cada efecto. Para agregar una habilidad nueva: un valor en AbilityData.Efecto y su caso acá.</summary>
    public static System.Type Tipo(AbilityData.Efecto efecto)
    {
        switch (efecto)
        {
            case AbilityData.Efecto.Trasbordo: return typeof(HabilidadTrasbordo);
            default: return null;
        }
    }

    /// <summary>Si en este momento se puede usar (fase de la ronda, vivo, etc.).</summary>
    public virtual bool PuedeUsar()
    {
        HealthSystem vida = GetComponent<HealthSystem>();
        if (vida != null && vida.currentHealth <= 0) return false;
        // US 019, CA6: no en la compra ni en la selección de personajes del Táctico.
        RondasTacticas rondas = RondasTacticas.Actual;
        if (rondas != null && rondas.Listo &&
            (rondas.FaseActual == RondasTacticas.Fase.Compra || rondas.FaseActual == RondasTacticas.Fase.Seleccion)) return false;
        if (BuyPhase.Current != null && BuyPhase.Current.IsActive) return false;
        // Ni plantando o desactivando el dispositivo (US 131 y US 132).
        if (DispositivoTactico.Manipulando) return false;
        return true;
    }

    /// <summary>Hace el efecto. Devuelve false si no se pudo (y entonces no se gasta la carga).</summary>
    public abstract bool Aplicar();
}
