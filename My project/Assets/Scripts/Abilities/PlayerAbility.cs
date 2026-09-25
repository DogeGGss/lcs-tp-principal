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

    private void Update()
    {
        if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - Time.deltaTime);
        if (ability != null && !ShopUI.IsOpen && Input.GetKeyDown(ability.key)) TryUse();
    }

    public bool TryUse()
    {
        if (!IsReady) return false;
        CooldownLeft = ability.cooldown;
        Used?.Invoke();
        return true;
    }
}
