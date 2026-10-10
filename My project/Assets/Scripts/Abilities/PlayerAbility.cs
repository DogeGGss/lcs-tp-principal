using UnityEngine;

// La habilidad del personaje (por ahora una sola): se activa con su tecla y gasta una carga, que después se recupera.
// Es la base de US 014 y US 017; el efecto de cada habilidad es un EfectoHabilidad que se agrega según AbilityData.efecto.
// US 019, CA5: con varias cargas, se recuperan de a una (la siguiente empieza a recargarse cuando termina la anterior).
public class PlayerAbility : MonoBehaviour
{
    [SerializeField] private AbilityData ability;

    public AbilityData Ability => ability;

    /// <summary>Segundos que faltan para recuperar la próxima carga (0 si están todas).</summary>
    public float CooldownLeft { get; private set; }
    public int Cargas { get; private set; }
    public int MaxCargas => ability == null ? 0 : Mathf.Max(1, ability.cargas);
    public bool Recargando => Cargas < MaxCargas;
    public bool IsReady => ability != null && Cargas > 0;

    // Cómo va la carga que se está recuperando: 0 recién usada, 1 lista (o todas cargadas).
    public float Progress => ability == null || !Recargando || ability.cooldown <= 0f ? 1f : 1f - CooldownLeft / ability.cooldown;

    public event System.Action Used;

    [Tooltip("Usar la habilidad del personaje elegido en la selección (US 015). Si no hay personajes, queda la de arriba.")]
    [SerializeField] private bool useSelectedCharacter = true;

    private EfectoHabilidad efecto;

    // US 015, CA6: el jugador empieza la partida con la habilidad del personaje que eligió.
    private void Awake()
    {
        if (useSelectedCharacter)
        {
            CharacterData character = CharacterRoster.Selected;
            if (character != null && character.ability != null) ability = character.ability;
        }
        Preparar();
    }

    /// <summary>US 016: el personaje se eligió con la partida ya cargada (Modo Táctico): se usa su habilidad.</summary>
    public void UsarPersonaje(CharacterData character)
    {
        if (!useSelectedCharacter || character == null || character.ability == null) return;
        ability = character.ability;
        Preparar();
    }

    /// <summary>Todas las cargas listas, sin nada recargándose (por ejemplo, al empezar una ronda del Táctico).</summary>
    public void Recargar()
    {
        Cargas = MaxCargas;
        CooldownLeft = 0f;
    }

    // Todas las cargas listas y el componente del efecto que corresponde (y ninguno de otra habilidad).
    private void Preparar()
    {
        Cargas = MaxCargas;
        CooldownLeft = 0f;
        System.Type tipo = ability == null ? null : EfectoHabilidad.Tipo(ability.efecto);
        if (efecto != null && (tipo == null || efecto.GetType() != tipo)) { Destroy(efecto); efecto = null; }
        if (efecto == null && tipo != null)
        {
            efecto = (EfectoHabilidad)GetComponent(tipo);
            if (efecto == null) efecto = (EfectoHabilidad)gameObject.AddComponent(tipo);
        }
        if (efecto != null) efecto.Habilidad = ability;
    }

    private void Update()
    {
        if (Recargando && ability != null)
        {
            CooldownLeft -= Time.deltaTime;
            if (CooldownLeft <= 0f)
            {
                Cargas++;
                CooldownLeft = Recargando ? CooldownLeft + ability.cooldown : 0f;
            }
        }
        if (ability != null && !ShopUI.IsOpen && KeyBindings.Down(GameAction.Habilidad)) TryUse();
    }

    public bool TryUse()
    {
        if (!IsReady) return false;
        // US 019, CA3 y CA6: si el efecto no se puede hacer (contra una pared, en la compra...), no se gasta la carga.
        if (efecto != null && (!efecto.PuedeUsar() || !efecto.Aplicar())) return false;
        if (!Recargando) CooldownLeft = ability.cooldown;
        Cargas--;
        Used?.Invoke();
        return true;
    }
}
