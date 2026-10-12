using UnityEngine;

// Un personaje jugable (F04, US 015): nombre, retrato, color y su habilidad.
// Los personajes se cargan solos desde Resources/Personajes: para sumar uno alcanza con crear el asset ahí
// (clic derecho > Create > Personajes > Personaje), sin tocar código.
[CreateAssetMenu(fileName = "Personaje", menuName = "Personajes/Personaje")]
public class CharacterData : ScriptableObject
{
    [Tooltip("Orden en la pantalla de selección (de menor a mayor).")]
    public int order;
    public string displayName = "Personaje";
    [Tooltip("Rol corto que se muestra debajo del nombre (por ejemplo, Iniciador).")]
    public string role;
    [Tooltip("Si queda vacío, la tarjeta muestra la inicial del nombre sobre el color del personaje.")]
    public Sprite portrait;
    public Color color = new Color(0.949f, 0.604f, 0.220f);
    [TextArea] public string description;
    public AbilityData ability;
    [Tooltip("Modelo del cuerpo (FBX humanoide, como los de Mixamo). Si queda vacío, el personaje usa el cuerpo de siempre. " +
             "Lo ven los demás jugadores; en primera persona se siguen viendo los brazos de siempre.")]
    public GameObject modelo;
    [Tooltip("Marcado mientras el personaje no esté definido en la sesión de diseño de F04.")]
    public bool provisional = true;
}
