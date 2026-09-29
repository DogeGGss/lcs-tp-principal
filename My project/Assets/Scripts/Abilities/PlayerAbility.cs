using UnityEngine;

// La habilidad del personaje (por ahora una sola): se activa con su tecla y después se recarga.
// Es la base de US 014 y US 017; el efecto de la habilidad se engancha al evento Used.
public class PlayerAbility : MonoBehaviour
{
    [SerializeField] private AbilityData ability;

    public AbilityData Ability => ability;
    public float CooldownLeft { get; private set; }
    public bool IsReady => ability != null && CooldownLeft <= 0f;

    // 0 recién usada, 1 lista.
    public float Progress => ability == null || ability.cooldown <= 0f ? 1f : 1f - CooldownLeft / ability.cooldown;

    public event System.Action Used;

    [Tooltip("Usar la habilidad del personaje elegido en la selección (US 015). Si no hay personajes, queda la de arriba.")]
    [SerializeField] private bool useSelectedCharacter = true;

    // US 015, CA6: el jugador empieza la partida con la habilidad del personaje que eligió.
    private void Awake()
    {
        if (!useSelectedCharacter) return;
        CharacterData character = CharacterRoster.Selected;
        if (character != null && character.ability != null) ability = character.ability;
    }

    private void Update()
    {
        if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - Time.deltaTime);
        if (ability != null && !ShopUI.IsOpen && KeyBindings.Down(GameAction.Habilidad)) TryUse();
    }

    public bool TryUse()
    {
        if (!IsReady) return false;
        CooldownLeft = ability.cooldown;
        Used?.Invoke();
        return true;
    }
}
