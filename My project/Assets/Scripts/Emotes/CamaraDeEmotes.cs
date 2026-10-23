using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// F27 (Emotes), US 263: mientras el jugador de esta computadora baila, la cámara sale a tercera persona para verse, como
// en Overwatch.
// - CA1: al empezar el baile, la cámara se aleja de la cabeza hacia atrás y un poco arriba en 0,3 s, mirando al
//   personaje. El cuerpo propio se ve entero (HideOwnBody.MostrarCuerpo) y bailando (BaileEnTerceraPersona, sin el IK
//   de los brazos de primera persona), y se esconden el arma y los brazos de primera persona (la cámara de las armas).
// - CA2: el mouse gira la cámara alrededor del personaje; el personaje no gira (CameraLook.Quieta).
// - CA3: no atraviesa paredes ni el piso: si hay algo atrás, se acerca.
// - CA4: al cortarse el baile vuelve a primera persona en 0,3 s, mirando hacia donde miraba antes de bailar, y vuelven
//   el arma y los brazos.
// - CA5: en tercera persona se esconden la mira y el arma del HUD (CombatHud.SinArma); la vida y el mapa siguen.
// Se arma sola al cargar la escena, como la rueda.
[DefaultExecutionOrder(1000)] // después de lo que mueve la cámara (CameraLook, PlayerMovement)
public class CamaraDeEmotes : MonoBehaviour
{
    private const float Pasar = 0.3f;                      // s para salir y volver
    private const float Distancia = 2.8f, Arriba = 0.35f;  // m atrás y arriba del pecho
    private const float AlturaPecho = 1.15f;               // m del pecho sobre los pies (lo que mira la cámara)
    private const float RadioDeLaCamara = 0.2f;            // para no meterse en las paredes
    private const float PitchInicial = 12f, PitchMinimo = -25f, PitchMaximo = 65f;

    private static CamaraDeEmotes unica;

    private PlayerMovement jugador;
    private Transform camara;
    private Vector3 lugarPrimera;      // la cámara en primera persona (local, respecto de su padre)
    private Quaternion giroPrimera;
    private float tercera;             // 0: primera persona; 1: tercera
    private bool activa;               // está a cargo de la cámara
    private float yaw, pitch;
    private float distanciaVista = Distancia;
    private BaileEnTerceraPersona baile;
    private HideOwnBody cuerpo;
    private readonly List<Camera> camarasDeArmas = new List<Camera>();
    private readonly List<Rig> capasIK = new List<Rig>();
    private readonly List<float> pesosIK = new List<float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (unica != null) return;
        var go = new GameObject("Cámara de emotes") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        unica = go.AddComponent<CamaraDeEmotes>();
    }

    // El IK de los brazos de primera persona, apagado mientras baila (antes de que se mueva el cuerpo; lo que lo prenda en
    // su Update ya corrió).
    private void Update()
    {
        if (!activa) return;
        foreach (Rig r in capasIK) if (r != null && r.weight != 0f) r.weight = 0f;
    }

    private void LateUpdate()
    {
        bool bailando = Emotes.Actual >= 0;
        if (!activa)
        {
            if (!bailando || !Empezar()) return;
        }
        if (jugador == null || camara == null) { Terminar(); return; }

        // El baile del cuerpo propio (el de los demás lo hace su copia).
        if (baile != null) baile.Baile = Emotes.Actual;

        tercera = Mathf.MoveTowards(tercera, bailando ? 1f : 0f, Time.deltaTime / Pasar);
        if (!bailando && tercera <= 0f) { Terminar(); return; }

        // CA2: el mouse gira alrededor del personaje (solo bailando; volviendo, ya no).
        CameraLook.Quieta = true;
        if (bailando && !PauseMenu.IsPaused && !RuedaDeEmotes.Abierta)
        {
            float sensibilidad = KeyBindings.Sensitivity;
            yaw += Input.GetAxis("Mouse X") * sensibilidad;
            pitch -= Input.GetAxis("Mouse Y") * sensibilidad * (KeyBindings.InvertY ? -1f : 1f);
            pitch = Mathf.Clamp(pitch, PitchMinimo, PitchMaximo);
        }

        // Primera persona: donde estaba la cámara (lo que cambie el resto, como agacharse, no cuenta: se corta el baile).
        camara.localPosition = lugarPrimera;
        camara.localRotation = giroPrimera;
        Vector3 desdeOjos = camara.position;
        Quaternion giroOjos = camara.rotation;

        // Tercera persona: atrás y un poco arriba, mirando al pecho; si hay algo en el medio, se acerca (CA3).
        Vector3 pecho = jugador.transform.position + jugador.transform.up * AlturaPecho;
        Quaternion orbita = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 hacia = orbita * Vector3.back;
        float lejos = Distancia;
        foreach (RaycastHit g in Physics.SphereCastAll(pecho, RadioDeLaCamara, hacia, Distancia, Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
            if (g.collider != null && !g.collider.transform.IsChildOf(jugador.transform) && g.distance < lejos) lejos = g.distance;
        lejos = Mathf.Max(0.4f, lejos);
        // Acercarse, enseguida (para no ver a través de la pared); alejarse, suave.
        distanciaVista = lejos < distanciaVista ? lejos : Mathf.Lerp(distanciaVista, lejos, 1f - Mathf.Exp(-6f * Time.deltaTime));
        Vector3 lugarTercera = pecho + hacia * distanciaVista + Vector3.up * (Arriba * distanciaVista / Distancia);
        Quaternion giroTercera = Quaternion.LookRotation(pecho - lugarTercera, Vector3.up);

        float k = tercera * tercera * (3f - 2f * tercera);
        camara.SetPositionAndRotation(Vector3.Lerp(desdeOjos, lugarTercera, k), Quaternion.Slerp(giroOjos, giroTercera, k));

        // CA1 y CA5: a mitad de camino se ve el cuerpo y se esconden el arma, los brazos y la mira.
        bool afuera = k > 0.25f;
        if (cuerpo != null) cuerpo.MostrarCuerpo(afuera);
        foreach (Camera c in camarasDeArmas) if (c != null && c.enabled == afuera) c.enabled = !afuera;
        CombatHud.SinArma = afuera;
    }

    // Toma la cámara del jugador de esta computadora.
    private bool Empezar()
    {
        Camera principal = Camera.main;
        jugador = principal != null ? principal.GetComponentInParent<PlayerMovement>() : null;
        if (jugador == null) return false;
        camara = principal.transform;
        lugarPrimera = camara.localPosition;
        giroPrimera = camara.localRotation;
        // Empieza atrás del personaje, hacia donde mira, un poco desde arriba.
        yaw = camara.eulerAngles.y;
        pitch = PitchInicial;
        distanciaVista = Distancia;
        tercera = 0f;

        Animator animador = jugador.animator;
        if (animador == null || !animador.isHuman)
            foreach (Animator a in jugador.GetComponentsInChildren<Animator>()) if (a.isHuman) { animador = a; break; }
        baile = animador != null ? animador.GetComponent<BaileEnTerceraPersona>() : null;
        if (baile == null && animador != null) baile = BaileEnTerceraPersona.Crear(animador.gameObject, animador, null);
        cuerpo = jugador.GetComponent<HideOwnBody>();

        // Las demás cámaras del jugador dibujan el arma y los brazos de primera persona.
        camarasDeArmas.Clear();
        foreach (Camera c in jugador.GetComponentsInChildren<Camera>()) if (c != principal && c.enabled) camarasDeArmas.Add(c);
        // El IK de los brazos de primera persona (los agarres del arma) se apaga: los mueve el baile.
        capasIK.Clear();
        pesosIK.Clear();
        foreach (Rig r in jugador.GetComponentsInChildren<Rig>())
            {
                capasIK.Add(r);
                pesosIK.Add(r.weight);
                r.weight = 0f;
            }
        activa = true;
        return true;
    }

    // Devuelve la cámara (CA4: en primera persona, mirando hacia donde miraba antes de bailar).
    private void Terminar()
    {
        activa = false;
        if (camara != null) { camara.localPosition = lugarPrimera; camara.localRotation = giroPrimera; }
        CameraLook.Quieta = RuedaDeEmotes.Abierta;
        CombatHud.SinArma = false;
        if (cuerpo != null) cuerpo.MostrarCuerpo(false);
        foreach (Camera c in camarasDeArmas) if (c != null) c.enabled = true;
        camarasDeArmas.Clear();
        for (int i = 0; i < capasIK.Count; i++) if (capasIK[i] != null) capasIK[i].weight = pesosIK[i];
        capasIK.Clear();
        pesosIK.Clear();
        if (baile != null) baile.Baile = -1;
        jugador = null;
        camara = null;
    }
}
