using UnityEngine;

/// <summary>Lo que deja la bola de veneno del jefe al caer (US 267).</summary>
public enum VenenoDelVuelo { Nubes, Charcos }

/// <summary>El dibujo de las marcas que brillan de un enemigo (US 283).</summary>
public enum PatronDeMarcas { Ninguno = 0, Manchas = 1, Aros = 2, RayasEnElLomo = 3 }

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

    [Header("Marcas que brillan (US 283)")]
    [Tooltip("El dibujo de las marcas bioluminiscentes, para que se vea de noche. Ninguno: sin marcas.")]
    public PatronDeMarcas marcas = PatronDeMarcas.Ninguno;
    [Tooltip("De qué color brillan.")]
    [ColorUsage(false, false)] public Color colorDeMarcas = new Color(0f, 0.9f, 1f);
    [Tooltip("Manchas y aros: cuántas entran a lo largo del cuerpo (más: más chicas y juntas). Las rayas usan las opciones de abajo.")]
    [Range(2f, 30f)] public float cantidadDeMarcas = 9f;
    [Tooltip("Manchas y aros: qué tan grandes son.")]
    [Range(0.1f, 1f)] public float tamanioDeMarcas = 0.6f;
    [Tooltip("Manchas y aros: qué parte de los lugares queda sin marca (0: todo marcado).")]
    [Range(0f, 0.95f)] public float huecosEntreMarcas = 0.35f;
    [Tooltip("Manchas y aros: qué tan irregulares son (0: redondas; 1: como de vaca, largas, torcidas y con el borde ondulado).")]
    [Range(0f, 1f)] public float irregularidadDeMarcas;
    [Tooltip("Cuánto brillan (más de 1 resplandece con el bloom del mapa).")]
    [Range(0f, 10f)] public float brilloDeMarcas = 2.2f;
    [Tooltip("Qué tan opacas son sobre la piel (bajas: se ven como de adentro del bicho; 1: pintadas encima).")]
    [Range(0f, 1f)] public float opacidadDeMarcas = 0.5f;
    [Tooltip("Cuánto respiran (suben y bajan de brillo despacio).")]
    [Range(0f, 1f)] public float pulsoDeMarcas = 0.35f;
    [Tooltip("Huesos del modelo cuyas partes no llevan marcas: lo que no es piel (la lanza del Catfish va en \"BN_ R_ Hand\").")]
    public string[] huesosSinMarcas = new string[0];
    [Tooltip("Rayas: distancia entre una raya y la siguiente, en el largo del cuerpo (1 = el cuerpo entero). Menos: más juntas.")]
    [Range(0.005f, 0.3f)] public float separacionDeRayas = 0.05f;
    [Tooltip("Rayas: qué tan anchas son, en el largo del cuerpo. Más que la separación: se tocan.")]
    [Range(0.001f, 0.1f)] public float anchoDeRayas = 0.015f;
    [Tooltip("Rayas: hasta dónde llegan hacia cada costado desde el medio del lomo, en el largo del cuerpo (terminan en punta).")]
    [Range(0.01f, 0.5f)] public float largoDeRayas = 0.08f;
    [Tooltip("Giro de las marcas (grados, en x, y, z) respecto del modelo: hacia dónde corren las rayas y dónde queda el lomo. " +
             "Las rayas cruzan el eje adelante (z) y van arriba (y): por ejemplo, y 90 las pone a lo largo del cuerpo; " +
             "x o z 90 cambian qué lado cuenta como lomo. Se ve en los enemigos que aparecen después.")]
    public Vector3 giroDeMarcas;
    [Tooltip("Ojos: dónde están en la textura del bicho (u, v: de 0 a 1, desde abajo a la izquierda). Brillan del color de las marcas.")]
    public Vector2 ojoEnLaTextura;
    [Tooltip("Ojos: qué tan grande es la zona del ojo en la textura (0: sin ojos que brillen).")]
    [Range(0f, 0.2f)] public float radioDelOjo;
    [Tooltip("Ojos: cuánto brillan (más de 1 resplandece con el bloom).")]
    [Range(0f, 10f)] public float brilloDeOjos = 4f;
    [Tooltip("Ojos: delineado de manchitas que brillan alrededor de cada ojo (0: sin delineado; 1: fuerte).")]
    [Range(0f, 1f)] public float delineadoDeOjos;
    [Tooltip("Ojos: brilla lo negro de esa zona de la textura (huecos, como los de la cabeza del Jefe) en vez de lo de color fuerte.")]
    public bool ojosOscuros;

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
    [Tooltip("Al empezar cada golpe cuerpo a cuerpo (US 037). Opcional.")]
    public AudioClip sonidoDeAtaque;
    [Tooltip("Jefe: el rugido antes de embestir (US 266) y al enfurecerse (US 268). Opcional.")]
    public AudioClip sonidoDeRugido;
    [Tooltip("Jefe: el zumbido de las alas, en loop todo el tiempo mientras vive (también en el piso, siempre aletea). Opcional.")]
    public AudioClip sonidoDeVuelo;
    [Tooltip("Jefe: al escupir cada bola de veneno (US 267, CA3). Opcional.")]
    public AudioClip sonidoDeEscupir;
    [Tooltip("Jefe: cuando la bola de veneno toca el piso y se arma la nube (o el charco) (US 267, CA4). Opcional.")]
    public AudioClip sonidoDeImpactoDeVeneno;

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
