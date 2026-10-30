using UnityEngine;

/// <summary>Lo que deja la bola de veneno del jefe al caer (US 267).</summary>
public enum VenenoDelVuelo { Nubes, Charcos }

// F11 (NPCs), US 145: un tipo de NPC enemigo del modo Zombie (los bichos del rift). Todo lo que distingue a un tipo
// está acá, como dato, y no en el código (CA5): se ajusta o se suma un tipo nuevo creando otro de estos
// (Create > Riftwalker > Tipo de NPC) en Resources/NPCs.
// - Común: Catfish, 100 de vida, golpea por 20, camina a 2,5 m/s (CA1).
// - Rápido: Wolf Boss achicado, 60 de vida, golpea por 10, corre a 5 m/s (CA2).
// - Tanque: Lizard agrandado, 400 de vida, golpea por 40, camina a 2,5 m/s (como el Común) y no se frena al recibir
//   impactos (CA3).
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
    [Tooltip("Giro del modelo (grados) para que mire hacia adelante: algunos modelos vienen mirando para atrás (el escarabajo del Jefe, 180).")]
    public float giroDelModelo;

    [Header("Combate")]
    [Tooltip("Vida al aparecer. No tienen escudo.")]
    [Min(1)] public int vida = 100;
    [Tooltip("Daño de cada golpe al jugador (US 037): se aplica primero al escudo y el resto a la vida.")]
    [Min(0)] public int danio = 20;
    [Tooltip("Cuánto se puede alejar el jugador, en m, más allá de donde llega la animación del golpe, y que igual le pegue (US 037, CA2). Más lejos, falla.")]
    [Min(0.1f)] public float alcance = 0.5f;
    [Tooltip("Qué tan rápido hace sus animaciones de ataque (1: como vienen; 1,5: un 50 % más rápido). El golpe entra antes en la misma proporción.")]
    [Range(0.5f, 3f)] public float velocidadDeAtaque = 1f;
    [Tooltip("Segundos entre golpes, como mínimo (US 037, CA4). El jefe, 1,5 (US 266).")]
    [Min(0.2f)] public float cadencia = 1f;
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

    [Header("Jefe (US 146)")]
    [Tooltip("Es un jefe: mientras está vivo se ve su barra de vida arriba de la pantalla (BarraDeJefe).")]
    public bool esJefe;
    [Tooltip("El nombre que se ve en la barra del jefe. Vacío: el nombre del tipo.")]
    public string titulo;
    [Tooltip("Si embiste (US 266): marca el camino con una línea roja y se lanza en línea recta.")]
    public bool embiste;
    [Tooltip("Animación mientras se prepara para embestir (el aviso, con la línea roja). Vacío: la de quieto en combate.")]
    public AnimationClip preparaEmbestida;
    [Tooltip("Animación mientras embiste. Vacío: la de correr.")]
    public AnimationClip embestida;
    [Tooltip("Si levanta vuelo y escupe veneno (US 267).")]
    public bool vuela;
    [Tooltip("Animación de vuelo (al subir, en el aire y al bajar). Vacío: la de caminar.")]
    public AnimationClip volar;
    [Tooltip("Animación al escupir cada bola de veneno (se mezcla con la de vuelo).")]
    public AnimationClip escupir;
    [Tooltip("Qué deja cada bola de veneno donde cae: una nube venenosa que lastima y tapa la vista, o un charco en el piso (US 267).")]
    public VenenoDelVuelo veneno = VenenoDelVuelo.Nubes;
    [Tooltip("Material de las partículas de la nube venenosa (el humo de la granada de humo). Vacío: sin partículas, solo la pantalla verde.")]
    public Material materialDeNube;
    [Tooltip("Si a la mitad de la vida se enfurece (US 268): ruge, se pone rojo y va más rápido y ataca más seguido.")]
    public bool furia;
    [Tooltip("Animación del rugido al enfurecerse.")]
    public AnimationClip rugido;

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
