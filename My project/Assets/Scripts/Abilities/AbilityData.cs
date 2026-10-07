using UnityEngine;

// Una habilidad de personaje (F04): cómo se llama, con qué tecla se usa, cuántas cargas tiene y cuánto tarda en recargarse.
// El efecto de cada habilidad es un componente (EfectoHabilidad) que PlayerAbility agrega según "efecto".
[CreateAssetMenu(fileName = "Habilidad", menuName = "Personajes/Habilidad")]
public class AbilityData : ScriptableObject
{
    // Qué hace la habilidad. Ninguno: solo se gasta y se recarga (las que todavía no tienen efecto).
    public enum Efecto { Ninguno, Trasbordo }

    public string displayName = "Habilidad";
    public Sprite icon;
    public KeyCode key = KeyCode.Q;
    [Tooltip("Segundos que tarda en recuperarse una carga después de usarla (las cargas vuelven de a una).")]
    public float cooldown = 20f;
    [Tooltip("Cuántas veces se puede usar seguidas antes de tener que esperar.")]
    [Min(1)] public int cargas = 1;
    public Efecto efecto = Efecto.Ninguno;
    [TextArea] public string description;
}
