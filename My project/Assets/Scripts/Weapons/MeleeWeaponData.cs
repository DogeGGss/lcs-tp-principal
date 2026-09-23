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
}
