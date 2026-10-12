using System.Collections.Generic;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Indicador de dirección del daño (US 192), como en Counter-Strike y Valorant.
// - CA1: al recibir daño aparece un arco rojo alrededor de la mira, del lado desde donde vino: arriba si vino de
//   adelante, abajo si vino de atrás, y a los costados si vino de la izquierda o de la derecha.
// - CA2: mientras se ve, el arco gira con el jugador y sigue apuntando al lugar desde donde le dispararon.
// - CA3: dura 1,5 s y se desvanece. Si recibe daño de varios lados, hay un arco por cada uno.
// - CA4: cuanto más daño hizo el golpe, más opaco es el arco.
// - CA5: el daño de una granada marca el lugar donde explotó (lo avisa Grenade1 con HealthSystem.DamageOrigin).
// - CA6: no muestra la distancia ni el nombre de quien disparó.
// - CA7: el daño de otros jugadores llega por la red con el lugar desde donde dispararon (JugadorEnRed.RpcDanio).
// Lo crea CombatHud sobre su lienzo. El lugar del daño lo avisa HealthSystem.Damaged.
public class IndicadorDeDano : MonoBehaviour
{
    private const float Duracion = 1.5f;          // CA3
    private const float Radio = 118f, Grosor = 9f, Apertura = 46f; // px en 1920 x 1080 y grados del arco
    private const int Tramos = 9, Maximo = 6;
    private const float DanoFuerte = 60f;         // CA4: de acá para arriba, el arco sale opaco del todo
    private const float MismoLugar = 2.5f;        // m: golpes desde el mismo lugar renuevan el mismo arco
    private static readonly Color Rojo = new Color(1f, 0.25f, 0.25f);

    private class Arco
    {
        public RectTransform giro;
        public Img[] tramos;
        public Vector3 lugar;
        public float desde = -10f, fuerza;
        public bool Activo => Time.time - desde < Duracion;
    }

    private readonly List<Arco> arcos = new List<Arco>();
    private HealthSystem jugador;
    private Transform vista;

    /// <summary>El jugador cuyo daño se muestra (el de esta computadora).</summary>
    public void Seguir(HealthSystem health)
    {
        jugador = health;
        Camera camara = health != null ? health.GetComponentInChildren<Camera>() : null;
        vista = camara != null ? camara.transform : health != null ? health.transform : null;
    }

    private void Awake()
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        for (int i = 0; i < Maximo; i++) arcos.Add(Crear());
    }

    private void OnEnable() => HealthSystem.Damaged += AlRecibirDano;
    private void OnDisable() => HealthSystem.Damaged -= AlRecibirDano;

    // Un arco: tramos rectos cortos sobre un círculo, centrados arriba. Se gira entero para apuntar al daño.
    private Arco Crear()
    {
        Arco arco = new Arco { giro = Node("Arco", transform), tramos = new Img[Tramos] };
        arco.giro.anchorMin = arco.giro.anchorMax = arco.giro.pivot = new Vector2(0.5f, 0.5f);
        arco.giro.sizeDelta = Vector2.zero;
        float largo = 2f * Mathf.PI * Radio * (Apertura / 360f) / Tramos + 0.8f;
        for (int i = 0; i < Tramos; i++)
        {
            float t = Tramos > 1 ? i / (float)(Tramos - 1) : 0.5f;
            float angulo = 90f + Mathf.Lerp(Apertura * 0.5f, -Apertura * 0.5f, t); // 90° = arriba
            float rad = angulo * Mathf.Deg2Rad;
            RectTransform tramo = Node("Tramo", arco.giro);
            tramo.anchorMin = tramo.anchorMax = tramo.pivot = new Vector2(0.5f, 0.5f);
            tramo.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Radio;
            tramo.localRotation = Quaternion.Euler(0f, 0f, angulo + 90f);
            // Más fino hacia las puntas, para que el arco termine en punta.
            float ancho = Grosor * Mathf.Lerp(0.35f, 1f, 1f - Mathf.Abs(t - 0.5f) * 2f);
            tramo.sizeDelta = new Vector2(largo, ancho);
            arco.tramos[i] = Image(tramo, null, Rojo);
        }
        arco.giro.gameObject.SetActive(false);
        return arco;
    }

    private void AlRecibirDano(HealthSystem quien, int dano, Vector3? origen)
    {
        if (quien == null || quien != jugador || !origen.HasValue || dano <= 0) return;
        // Demasiado cerca del propio jugador no hay una dirección que marcar.
        Vector3 hacia = origen.Value - quien.transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude < 0.04f) return;

        // Mismo lugar que un arco que se está viendo: se renueva. Si no, uno libre (o el más viejo). CA3
        Arco elegido = null;
        foreach (Arco a in arcos)
            if (a.Activo && (a.lugar - origen.Value).sqrMagnitude < MismoLugar * MismoLugar) { elegido = a; break; }
        float fuerza = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(dano / DanoFuerte)); // CA4
        if (elegido != null) fuerza = Mathf.Max(fuerza, elegido.fuerza * Restante(elegido));
        else
        {
            foreach (Arco a in arcos)
                if (elegido == null || !a.Activo && elegido.Activo || a.Activo == elegido.Activo && a.desde < elegido.desde) elegido = a;
        }
        elegido.lugar = origen.Value;
        elegido.fuerza = fuerza;
        elegido.desde = Time.time;
        elegido.giro.gameObject.SetActive(true);
        Colocar(elegido);
    }

    private static float Restante(Arco a) => Mathf.Clamp01(1f - (Time.time - a.desde) / Duracion);

    private void LateUpdate()
    {
        foreach (Arco a in arcos)
        {
            if (!a.giro.gameObject.activeSelf) continue;
            if (!a.Activo || jugador == null) { a.giro.gameObject.SetActive(false); continue; }
            Colocar(a);
        }
    }

    // CA1 y CA2: el arco apunta al lugar del daño respecto de hacia dónde mira el jugador ahora.
    private void Colocar(Arco a)
    {
        if (vista == null || jugador == null) return;
        Vector3 hacia = a.lugar - jugador.transform.position, frente = vista.forward;
        hacia.y = 0f;
        frente.y = 0f;
        if (hacia.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f)
        {
            float angulo = Vector3.SignedAngle(frente, hacia, Vector3.up); // positivo: a la derecha
            a.giro.localRotation = Quaternion.Euler(0f, 0f, -angulo);
        }
        // Se mantiene un momento y después se desvanece.
        float vida = Restante(a);
        Color color = WithAlpha(Rojo, a.fuerza * Mathf.Clamp01(vida / 0.6f));
        foreach (Img tramo in a.tramos) tramo.color = color;
    }
}
