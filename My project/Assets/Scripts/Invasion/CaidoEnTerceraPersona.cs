using TMPro;
using UnityEngine;

// F31 (Invasión cooperativa), US 357, CA8: lo que ven los demás de un compañero caído (la copia de JugadorEnRed).
// Las animaciones son de Mixamo (Personajes/Caido) en la capa "Caido" del Animator de los personajes, que tapa a todo
// el cuerpo y está apagada (peso 0) salvo mientras está caído o levantándose; la arma sola el editor
// (AnimacionesDeCaido). Al caer hace la de caer y sigue con la de arrastrarse (quieta si no se mueve); al revivir, la de
// levantarse. Mientras tanto no tiene el arma en la mano.
// CA2: arriba de él se ve que está caído, con la cuenta (o que lo están reviviendo).
public class CaidoEnTerceraPersona : MonoBehaviour
{
    public const string Capa = "Caido", Caer = "Caer", Arrastrarse = "Arrastrarse", Levantarse = "Levantarse";
    public const string Parametro = "Arrastre"; // la velocidad de la de arrastrarse: 0 quieto, 1 arrastrándose
    private const float Entrar = 0.15f, Salir = 0.35f, LevantarseSinClip = 2.5f;
    private const float AlturaDelCartel = 1.1f;

    private Animator animador;
    private ArmaEnTerceraPersona arma;
    private JugadorEnRed jugador;
    private int capa = -1;
    private bool hayParametro, buscado;
    private float peso, levantandoHasta = -1f, largoLevantarse = -1f;
    private bool estaba;
    private Vector3 antes;
    private TextMeshPro cartel;

    /// <summary>Está caído (lo dice su dueño, por la red).</summary>
    public bool Caido { get; set; }

    /// <summary>Se ve caído o levantándose: el arma no se tiene que ver.</summary>
    public bool Activo => peso > 0f;

    public static CaidoEnTerceraPersona Crear(GameObject personaje, Animator animador, ArmaEnTerceraPersona arma, JugadorEnRed jugador)
    {
        if (personaje == null) return null;
        CaidoEnTerceraPersona c = personaje.AddComponent<CaidoEnTerceraPersona>();
        c.animador = animador;
        c.arma = arma;
        c.jugador = jugador;
        c.antes = personaje.transform.position;
        return c;
    }

    private void Update()
    {
        Animar();
        Cartel();
        antes = transform.position;
    }

    private void Animar()
    {
        if (animador == null) return;
        if (!buscado)
        {
            buscado = true;
            capa = animador.GetLayerIndex(Capa);
            foreach (AnimatorControllerParameter p in animador.parameters) if (p.name == Parametro) hayParametro = true;
        }
        if (capa < 0) return; // (todavía no se armó la capa: Riftwalker > Invasión > Armar las animaciones de caído)

        if (Caido && !estaba)
        {
            animador.CrossFadeInFixedTime(Caer, 0.1f, capa, 0f); // (al terminar sigue sola con la de arrastrarse)
            estaba = true;
            levantandoHasta = -1f;
        }
        else if (!Caido && estaba)
        {
            estaba = false;
            animador.CrossFadeInFixedTime(Levantarse, 0.15f, capa, 0f); // CA4: se levanta
            levantandoHasta = Time.time + LargoDeLevantarse();
        }
        if (Caido && hayParametro)
        {
            float v = (transform.position - antes).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            animador.SetFloat(Parametro, v > 0.2f ? 1f : 0f, 0.15f, Time.deltaTime);
        }

        bool prendida = Caido || Time.time < levantandoHasta;
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, Time.deltaTime / (prendida ? Entrar : Salir));
        animador.SetLayerWeight(capa, peso * peso * (3f - 2f * peso));
    }

    private float LargoDeLevantarse()
    {
        if (largoLevantarse > 0f) return largoLevantarse;
        largoLevantarse = LevantarseSinClip;
        if (animador.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animador.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == Levantarse) { largoLevantarse = clip.length; break; }
        return largoLevantarse;
    }

    // Sin las manos en el arma mientras está en el piso (como al bailar, US 262).
    private void LateUpdate()
    {
        if (arma != null && peso > 0f) arma.Bailando = true;
    }

    // CA2: "Caído 23 s" (o "Reviviendo") arriba de él, mirando a la cámara.
    private void Cartel()
    {
        bool ver = Caido && jugador != null;
        if (!ver)
        {
            if (cartel != null) cartel.gameObject.SetActive(false);
            return;
        }
        if (cartel == null)
        {
            var go = new GameObject("Caído (cartel)");
            go.transform.SetParent(transform, false);
            cartel = go.AddComponent<TextMeshPro>();
            cartel.alignment = TextAlignmentOptions.Center;
            cartel.fontSize = 3f;
            cartel.rectTransform.sizeDelta = new Vector2(4f, 1f);
            if (MatchHud.Instance != null && MatchHud.Instance.LabelFont != null) cartel.font = MatchHud.Instance.LabelFont;
        }
        cartel.gameObject.SetActive(true);
        cartel.text = CaidaEnInvasion.CartelDe(jugador.Actor);
        cartel.transform.position = transform.position + Vector3.up * AlturaDelCartel;
        Camera cam = Camera.main;
        if (cam != null) cartel.transform.rotation = Quaternion.LookRotation(cartel.transform.position - cam.transform.position);
    }

    private void OnDisable()
    {
        if (animador != null && capa >= 0) animador.SetLayerWeight(capa, 0f);
        if (cartel != null) cartel.gameObject.SetActive(false);
    }
}
