using UnityEngine;

// Zona de impacto de un personaje (US 165): cabeza, cuerpo o piernas. Va en un collider trigger
// pegado al hueso, así baja con el cuerpo al agacharse y no molesta al movimiento; las balas la
// encuentran igual (WeaponFire). La vida (HealthSystem) está en el padre.
// Va junto a un collider (caja, esfera o cápsula) marcado como trigger.
public class HitZone : MonoBehaviour
{
    public BodyZone zone = BodyZone.Body;

    private void Reset()
    {
        Collider area = GetComponent<Collider>();
        if (area != null) area.isTrigger = true;
    }
}
