using UnityEngine;

// Una habilidad de personaje (F04): cómo se llama, con qué tecla se usa y cuánto tarda en recargarse.
// El efecto de cada habilidad se engancha al evento Used de PlayerAbility.
[CreateAssetMenu(fileName = "Habilidad", menuName = "Personajes/Habilidad")]
public class AbilityData : ScriptableObject
{
    public string displayName = "Habilidad";
    public Sprite icon;
    public KeyCode key = KeyCode.Q;
    [Tooltip("Segundos que tarda en volver a estar disponible después de usarla.")]
    public float cooldown = 20f;
    [TextArea] public string description;
}
