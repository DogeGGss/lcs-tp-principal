using UnityEngine;

[CreateAssetMenu(fileName = "NuevaArmaMelee", menuName = "Armas/Arma Melee")]
public class MeleeWeaponData : ScriptableObject
{
    public string weaponName = "Cuchillo";
    public GameObject viewModelPrefab;

    public int damage = 50;
    [Tooltip("Segundos entre un golpe y el siguiente")]
    public float cooldown = 0.75f;
    [Tooltip("Distancia maxima del golpe, en metros")]
    public float range = 2f;
    [Tooltip("Angulo total del cono de ataque, en grados")]
    [Range(0f, 180f)] public float attackAngle = 60f;
    [Tooltip("Multiplicador de velocidad mientras esta equipada (1 = sin bonus)")]
    public float moveSpeedMultiplier = 1.15f;

    /// <summary>El multiplicador que se usa en la partida: en el Deathmatch el cuchillo no da velocidad extra (se mueve
    /// a la velocidad normal, 1×), así nadie cruza el mapa con el cuchillo en la mano para llegar antes.</summary>
    public float MoveSpeedMultiplier => MatchSettings.Mode == GameMode.Deathmatch ? Mathf.Min(1f, moveSpeedMultiplier) : moveSpeedMultiplier;
    [Tooltip("Sonido al sacar el arma con su tecla (US 114)")]
    public AudioClip drawSound;
}
