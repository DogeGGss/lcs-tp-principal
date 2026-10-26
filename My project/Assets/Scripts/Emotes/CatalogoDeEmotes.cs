using System.Collections.Generic;
using UnityEngine;

// F27 (Emotes): los bailes que hay en el juego, en orden (el lugar en la lista es el número que viaja por la red).
// Es un asset (Resources/Emotes): el editor le agrega solo cada baile nuevo que se pone en Personajes/Emotes
// (AnimacionesDeEmotes), con el nombre del archivo; acá se le cambia el nombre que se muestra y se le pone el ícono.
// No se reordena: cambiar el orden cambia qué baile es cada número.
[CreateAssetMenu(menuName = "Riftwalker/Catálogo de emotes")]
public class CatalogoDeEmotes : ScriptableObject
{
    [System.Serializable]
    public class Emote
    {
        [Tooltip("El estado de la capa \"Emotes\" del Animator de los personajes (el nombre del archivo de la animación).")]
        public string estado = "";
        [Tooltip("El nombre que se muestra (en la rueda y en el perfil).")]
        public string nombre = "";
        [Tooltip("El ícono (en la rueda y en el perfil).")]
        public Sprite icono;
        [Tooltip("La animación (para la vista previa del perfil, US 264). La pone el editor.")]
        public AnimationClip clip;
    }

    public List<Emote> emotes = new List<Emote>();

    private static CatalogoDeEmotes actual;

    /// <summary>El asset de Resources (o uno vacío, si falta).</summary>
    public static CatalogoDeEmotes Actual
    {
        get
        {
            if (actual != null) return actual;
            actual = Resources.Load<CatalogoDeEmotes>("Emotes");
            if (actual == null) actual = CreateInstance<CatalogoDeEmotes>();
            return actual;
        }
    }

    /// <summary>El emote con ese número, o null.</summary>
    public Emote De(int indice) => indice >= 0 && indice < emotes.Count ? emotes[indice] : null;
}
