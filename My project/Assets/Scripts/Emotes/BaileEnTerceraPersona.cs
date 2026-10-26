using UnityEngine;

// F27 (Emotes), US 262: el personaje que ven los demás bailando (las copias de JugadorEnRed y el muñeco espejo). Los
// bailes son animaciones de Mixamo (Personajes/Emotes) en la capa "Emotes" del Animator de los personajes, que tapa a
// todo el cuerpo y está apagada (peso 0) salvo mientras baila; la arma sola el editor (AnimacionesDeEmotes). Como son
// humanoides, sirven para todos los personajes.
// CA3: mientras baila, el arma se guarda (no se ve: lo esconde el que maneja la copia) y las manos dejan el arma
// (ArmaEnTerceraPersona.Bailando).
public class BaileEnTerceraPersona : MonoBehaviour
{
    public const string Capa = "Emotes";
    private const float Entrar = 0.25f, Salir = 0.3f;

    private Animator animador;
    private ArmaEnTerceraPersona arma;
    private int capa = -1;
    private float peso;
    private int bailando = -1;

    /// <summary>El baile que tiene que hacer (su número en el catálogo), o -1.</summary>
    public int Baile { get; set; } = -1;

    /// <summary>Está bailando (o terminando de dejar de bailar): el arma no se tiene que ver.</summary>
    public bool Bailando => peso > 0f;

    public static BaileEnTerceraPersona Crear(GameObject personaje, Animator animador, ArmaEnTerceraPersona arma)
    {
        if (personaje == null || animador == null) return null;
        BaileEnTerceraPersona b = personaje.AddComponent<BaileEnTerceraPersona>();
        b.animador = animador;
        b.arma = arma;
        return b;
    }

    private void Update()
    {
        if (animador == null) return;
        if (capa < 0) capa = animador.GetLayerIndex(Capa);
        if (capa < 0) return;
        if (Baile != bailando)
        {
            CatalogoDeEmotes.Emote e = CatalogoDeEmotes.Actual.De(Baile);
            if (e != null) animador.CrossFadeInFixedTime(e.estado, peso > 0.01f ? 0.2f : 0f, capa, 0f);
            bailando = e != null ? Baile : -1;
        }
        bool prendida = bailando >= 0;
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, Time.deltaTime / (prendida ? Entrar : Salir));
        animador.SetLayerWeight(capa, peso * peso * (3f - 2f * peso));
        if (arma != null) arma.Bailando = Bailando;
    }

    private void OnDisable()
    {
        if (animador != null && capa >= 0) animador.SetLayerWeight(capa, 0f);
        if (arma != null) arma.Bailando = false;
    }
}
