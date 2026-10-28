using UnityEngine;

// F11 (NPCs), US 145: un tipo de NPC enemigo del modo Zombie (los bichos del rift). Todo lo que distingue a un tipo
// está acá, como dato, y no en el código (CA5): se ajusta o se suma un tipo nuevo creando otro de estos
// (Create > Riftwalker > Tipo de NPC) en Resources/NPCs.
// - Común: Catfish, 100 de vida, golpea por 20, camina a 2,5 m/s (CA1).
// - Rápido: Wolf Boss achicado, 60 de vida, golpea por 10, corre a 5 m/s (CA2).
// - Tanque: Lizard agrandado, 400 de vida, golpea por 40, a 1,8 m/s y no se frena al recibir impactos (CA3).
// Cada uno con su modelo y su tamaño, así se reconocen de lejos (CA4).
// Lo usan Npc (que arma el NPC con su modelo, su vida y su tamaño) y las US que lo mueven, lo hacen atacar, recibir
// daño y morir (US 035 a 039).
[CreateAssetMenu(menuName = "Riftwalker/Tipo de NPC", fileName = "TipoDeNpc")]
public class TipoDeNpc : ScriptableObject
{
    [Tooltip("Cómo se llama el tipo (se ve en el juego, por ejemplo en el marcador o en la barra de vida).")]
    public string nombre = "Común";

    [Header("Aspecto (CA4)")]
    [Tooltip("El prefab del bicho, con su Animator (sus animaciones de caminar, correr, atacar, recibir golpe y morir).")]
    public GameObject modelo;
    [Tooltip("Tamaño respecto del modelo original: así se distinguen de lejos (el Rápido más chico, el Tanque más grande).")]
    [Min(0.1f)] public float escala = 1f;

    [Header("Combate")]
    [Tooltip("Vida al aparecer. No tienen escudo.")]
    [Min(1)] public int vida = 100;
    [Tooltip("Daño de cada golpe al jugador (US 037): se aplica primero al escudo y el resto a la vida.")]
    [Min(0)] public int danio = 20;
    [Tooltip("Si al recibir un impacto se frena un instante (US 038, CA3). El Tanque y el Jefe no se frenan.")]
    public bool frenaAlRecibirImpacto = true;

    [Header("Movimiento")]
    [Tooltip("Velocidad (m/s) con la que va hacia el jugador (US 035, CA2).")]
    [Min(0.1f)] public float velocidad = 2.5f;
    [Tooltip("Si con esa velocidad corre (animación de correr) o camina.")]
    public bool corre;

    [Header("Animaciones (las del modelo)")]
    [Tooltip("Quieto, sin el jugador cerca (o con el jugador muerto).")]
    public AnimationClip quieto;
    [Tooltip("Quieto frente al jugador, listo para atacar.")]
    public AnimationClip quietoEnCombate;
    [Tooltip("Caminar (si no corre).")]
    public AnimationClip caminar;
    [Tooltip("Correr (si corre, o si es lo único que tiene).")]
    public AnimationClip correr;
    [Tooltip("Golpes al jugador (US 037): se usa uno al azar.")]
    public AnimationClip[] ataques;
    [Tooltip("Al recibir un impacto (US 038).")]
    public AnimationClip golpeRecibido;
    [Tooltip("Morir (US 039).")]
    public AnimationClip morir;
    [Tooltip("Aturdido por la flash (US 038, CA2). Si no tiene, se queda quieto.")]
    public AnimationClip aturdido;

    [Header("Sonido")]
    [Tooltip("Grito al ver al jugador de cerca (US 036, CA2). Opcional.")]
    public AudioClip grito;

    [Header("Puntos (US 147)")]
    [Tooltip("Puntos por matarlo (US 039, CA3).")]
    [Min(0)] public int puntosPorBaja = 60;
    [Tooltip("Puntos por matarlo si el último impacto fue a la cabeza (US 039, CA3).")]
    [Min(0)] public int puntosPorBajaALaCabeza = 100;

    /// <summary>Todos los tipos, de Resources/NPCs, en el orden de su vida (de menos a más).</summary>
    public static TipoDeNpc[] Todos()
    {
        TipoDeNpc[] tipos = Resources.LoadAll<TipoDeNpc>("NPCs");
        System.Array.Sort(tipos, (a, b) => a.vida.CompareTo(b.vida));
        return tipos;
    }

    /// <summary>El tipo con ese nombre (sin importar mayúsculas), o null.</summary>
    public static TipoDeNpc Buscar(string nombre)
    {
        foreach (TipoDeNpc t in Resources.LoadAll<TipoDeNpc>("NPCs"))
            if (string.Equals(t.nombre, nombre, System.StringComparison.OrdinalIgnoreCase) || t.name == nombre) return t;
        return null;
    }
}
