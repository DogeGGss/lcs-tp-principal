using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// F11 (NPCs), US 285 y US 286: los enemigos que atacan de lejos (los Watcher, que flotan).
// - Se mantienen a distancia: si el jugador está a más de distanciaMaxima o no lo ven, se acercan (la persecución de
//   siempre); entre distanciaMinima y distanciaMaxima, viéndolo, se quedan flotando, lo miran y le disparan; más cerca
//   que distanciaMinima retroceden por el NavMesh; y si igual lo alcanzan, muerden (el ataque cuerpo a cuerpo, US 037).
// - US 285, rayo del ojo (Watcher rojo, francotirador): el ojo se carga 1 s mientras una mira roja fina sigue al jugador;
//   el último cuarto de segundo la mira se queda quieta (se puede esquivar) y dispara un rayo instantáneo que frena la
//   primera pared. Si le pega al jugador, le hace su daño.
// - US 286, ráfaga de bolas (Watcher verde): se carga medio segundo y escupe 3 bolas de energía, una cada 0,28 s,
//   apuntando a donde está el jugador en ese momento. Van derecho y se ven venir, así se pueden esquivar; chocan con las
//   paredes.
// - Mientras disparan vuelan alrededor del jugador (de costado, cambiando de sentido de a ratos o si algo los frena),
//   acomodándose a mitad de su distancia, y de a ratos toman más altura (hasta 2,5 m más) y vuelven a bajar.
// - Flotan a alturaDeVuelo con un leve vaivén; al morir caen al piso. Aturdidos (la flash, US 038) no disparan.
// Los valores salen de TipoDeNpc (sección "A distancia"). Lo agrega Npc.Crear (después de la persecución: la usa).
[RequireComponent(typeof(Npc))]
public class DistanciaDeNpc : MonoBehaviour
{
    private const float CargaRayo = 1f, MiraQuieta = 0.25f, LargoRayo = 60f;
    private const float RadioDelRayo = 0.3f;  // m: el grosor que cuenta para pegarle (el que se ve es Rayo.Ancho)
    private const float CargaBolas = 0.45f, EntreBolas = 0.28f;
    private const int Bolas = 3;
    private const float Retrocede = 3f;       // m/s hacia atrás
    private const float Vaiven = 0.12f;       // m que sube y baja flotando
    private const float CaeEn = 0.4f;         // s al morir
    private const float Rodea = 2.2f;         // m/s de costado alrededor del jugador
    private const float SubeHasta = 2.5f, Sube = 1.2f; // m de altura extra que toma de a ratos, y a cuántos m/s

    private enum Fase { Libre, Carga, Rafaga }

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private Fase fase = Fase.Libre;
    private float faseDesde, proximoDisparo, proximaBola;
    private int bolasTiradas;
    private bool manejo;              // puso Ocupado/Controlado en la persecución
    private Transform ojo, boca;
    private LineRenderer mira;
    private Vector3 apuntaA;          // a dónde va el rayo (la mira quieta)
    private float altura, vaivenDesde;
    private float sentido = 1f, cambiaSentido, extra, metaExtra, cambiaAltura;
    private CapsuleCollider capsula;
    private Vector3 centroCapsula;
    private Material materialLinea;

    private void Awake()
    {
        npc = GetComponent<Npc>();
        proximoDisparo = Time.time + Random.Range(1f, 2.5f);
        vaivenDesde = Random.Range(0f, 10f);
    }

    private void Start()
    {
        altura = npc.Tipo != null ? npc.Tipo.alturaDeVuelo : 0f;
        Transform modelo = npc.Modelo != null ? npc.Modelo : transform;
        ojo = Buscar(modelo, "EyeBall") ?? Buscar(modelo, "Eye") ?? Buscar(modelo, "Head");
        boca = Buscar(modelo, "Jaw") ?? ojo;
        capsula = GetComponent<CapsuleCollider>();
        if (capsula != null) centroCapsula = capsula.center;
        sentido = Random.value < 0.5f ? -1f : 1f;
        cambiaSentido = Time.time + Random.Range(3f, 6f);
        cambiaAltura = Time.time + Random.Range(2f, 5f);
    }

    private static Transform Buscar(Transform raiz, string parte)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.IndexOf(parte, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
        return null;
    }

    private void Update()
    {
        if (persecucion == null) persecucion = GetComponent<PersecucionDeNpc>();
        if (npc == null || npc.Tipo == null || persecucion == null) return;

        if (!npc.Vivo) { Cancelar(); Caer(); return; }
        Flotar();
        if (npc.Aturdido) { Cancelar(); Soltar(); return; }

        if (persecucion.Ocupado && !manejo) return; // otro lo está usando (mordiendo, US 037)
        Transform jugador = persecucion.Objetivo;
        if (jugador == null) { Cancelar(); Soltar(); return; }
        Vector3 hacia = jugador.position - transform.position;
        hacia.y = 0f;
        float lejos = hacia.magnitude;

        // Disparando: sigue hasta terminar (mirándolo).
        if (fase != Fase.Libre)
        {
            Manejar(true);
            Rodear(hacia, lejos, 0.5f); // más despacio mientras carga y dispara
            Mirar(hacia);
            if (fase == Fase.Carga) Cargar(jugador); else Rafaga(jugador);
            return;
        }

        if (persecucion.EnDistanciaDeAtaque) { Soltar(); return; } // lo alcanzó: muerde (US 037)
        if (lejos < npc.Tipo.distanciaMinima) { Retroceder(hacia); return; }
        if (lejos <= npc.Tipo.distanciaMaxima && LoVeDesde(OjoPos(), jugador))
        {
            Manejar(true); // vuela alrededor de él, mirándolo
            Rodear(hacia, lejos, 1f);
            Mirar(hacia);
            Animar(npc.Tipo.caminar != null ? npc.Tipo.caminar : npc.Tipo.quietoEnCombate);
            if (Time.time >= proximoDisparo) Empezar(jugador);
            return;
        }
        Soltar(); // lejos o sin verlo: se acerca (la persecución de siempre)
    }

    // ---------- Moverse ----------

    private void Manejar(bool mueve)
    {
        persecucion.Ocupado = true;
        persecucion.Controlado = mueve;
        manejo = true;
    }

    private void Soltar()
    {
        if (!manejo) return;
        manejo = false;
        animando = null; // la persecución vuelve a animarlo
        persecucion.Ocupado = false;
        persecucion.Controlado = false;
    }

    private void Retroceder(Vector3 hacia)
    {
        Manejar(true);
        NavMeshAgent agente = persecucion.Agente;
        if (agente != null && hacia.sqrMagnitude > 1e-4f)
            agente.Move(-hacia.normalized * Retrocede * Time.deltaTime);
        Mirar(hacia);
        Animar(npc.Tipo.caminar != null ? npc.Tipo.caminar : npc.Tipo.quietoEnCombate);
        if (Time.time >= proximoDisparo && persecucion.Objetivo != null && LoVeDesde(OjoPos(), persecucion.Objetivo))
            Empezar(persecucion.Objetivo); // retrocediendo también dispara
    }

    // De costado alrededor del jugador, acomodándose a mitad de su distancia. Si algo lo frena, cambia de sentido.
    private void Rodear(Vector3 hacia, float lejos, float cuanto)
    {
        NavMeshAgent agente = persecucion.Agente;
        if (agente == null || hacia.sqrMagnitude < 1e-4f) return;
        if (Time.time >= cambiaSentido) { sentido = -sentido; cambiaSentido = Time.time + Random.Range(3f, 6f); }
        Vector3 alJugador = hacia / lejos;
        Vector3 costado = Vector3.Cross(Vector3.up, alJugador) * sentido;
        float ideal = (npc.Tipo.distanciaMinima + npc.Tipo.distanciaMaxima) * 0.5f;
        float acomoda = Mathf.Clamp((lejos - ideal) / 3f, -1f, 1f) * 0.6f;
        Vector3 paso = (costado + alJugador * acomoda) * (Rodea * cuanto * Time.deltaTime);
        Vector3 antes = transform.position;
        agente.Move(paso);
        Vector3 movido = transform.position - antes;
        movido.y = 0f;
        if (paso.magnitude > 0.005f && movido.magnitude < paso.magnitude * 0.3f) // trabado: para el otro lado
        {
            sentido = -sentido;
            cambiaSentido = Time.time + Random.Range(3f, 6f);
        }
    }

    private void Mirar(Vector3 hacia)
    {
        if (hacia.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(hacia.normalized), 360f * Time.deltaTime);
    }

    // Flota con un vaivén y, de a ratos, toma más altura y vuelve a bajar (si no tiene techo). La cápsula (para los
    // disparos) sube con él.
    private void Flotar()
    {
        if (npc.Modelo == null || altura <= 0f) return;
        if (Time.time >= cambiaAltura)
        {
            cambiaAltura = Time.time + Random.Range(3f, 7f);
            metaExtra = metaExtra > 0.1f || Random.value < 0.4f ? 0f : Random.Range(1f, SubeHasta);
            if (metaExtra > 0f)
                foreach (RaycastHit techo in Physics.RaycastAll(transform.position + Vector3.up * (altura + 0.5f), Vector3.up,
                             metaExtra + 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (techo.collider.transform.IsChildOf(transform) || techo.collider.GetComponentInParent<Npc>() != null) continue;
                    metaExtra = Mathf.Min(metaExtra, Mathf.Max(0f, techo.distance - 1.5f));
                }
        }
        extra = Mathf.MoveTowards(extra, metaExtra, Sube * Time.deltaTime);
        Vector3 p = npc.Modelo.localPosition;
        p.y = altura + extra + Mathf.Sin((Time.time + vaivenDesde) * 2f) * Vaiven;
        npc.Modelo.localPosition = p;
        if (capsula != null) capsula.center = centroCapsula + Vector3.up * extra;
    }

    private void Caer()
    {
        if (npc.Modelo == null || altura <= 0f) return;
        Vector3 p = npc.Modelo.localPosition;
        p.y = Mathf.MoveTowards(p.y, 0f, Mathf.Max(altura, p.y) / CaeEn * Time.deltaTime);
        npc.Modelo.localPosition = p;
        if (p.y <= 0f) enabled = false;
    }

    private string animando;
    private void Animar(AnimationClip clip)
    {
        if (clip == null || npc.Animaciones == null || animando == clip.name) return;
        animando = clip.name;
        npc.Animaciones.Poner(clip, true, 0.2f);
        npc.Animaciones.Velocidad(1f);
    }

    // ---------- Disparar ----------

    private Vector3 OjoPos() => ojo != null ? ojo.position : transform.position + Vector3.up * (altura + 0.5f);
    private Vector3 BocaPos() => boca != null ? boca.position : OjoPos();
    // El pecho del jugador: por su CharacterController (su transform puede estar en los pies o en el medio del cuerpo).
    private static Vector3 Pecho(Transform jugador)
    {
        CharacterController cc = jugador.GetComponent<CharacterController>();
        if (cc != null) { Bounds b = cc.bounds; return b.center + Vector3.up * (b.extents.y * 0.35f); }
        return jugador.position + Vector3.up * 1.1f;
    }

    private void Empezar(Transform jugador)
    {
        fase = Fase.Carga;
        faseDesde = Time.time;
        bolasTiradas = 0;
        apuntaA = Pecho(jugador);
        SonidoDeNpc.Uno(npc.Tipo.sonidoDeCarga, transform, 35f);
        Animar(npc.Tipo.quietoEnCombate != null ? npc.Tipo.quietoEnCombate : npc.Tipo.quieto);
    }

    private void Cargar(Transform jugador)
    {
        float t = Time.time - faseDesde;
        if (npc.Tipo.aDistancia == AtaqueADistancia.RayoDelOjo)
        {
            // US 285: la mira sigue al jugador; el último cuarto de segundo se queda quieta.
            if (t < CargaRayo - MiraQuieta) apuntaA = Pecho(jugador);
            Vector3 desde = OjoPos();
            Vector3 fin = HastaDondeLlega(desde, apuntaA);
            float latido = 0.5f + 0.5f * Mathf.Sin(t * 30f);
            DibujarMira(desde, fin, Mathf.Lerp(0.03f, 0.09f, t / CargaRayo), Color.white * Mathf.Lerp(0.25f, 0.6f + 0.4f * latido, t / CargaRayo));
            if (t >= CargaRayo) DispararRayo();
        }
        else
        {
            // US 286: se carga y empieza la ráfaga.
            if (t >= CargaBolas) { fase = Fase.Rafaga; proximaBola = Time.time; }
        }
    }

    private void Rafaga(Transform jugador)
    {
        if (Time.time < proximaBola) return;
        Vector3 desde = BocaPos() + transform.forward * 0.3f;
        BolaDeEnergia.Lanzar(desde, Pecho(jugador), npc.Tipo, transform);
        SonidoDeNpc.Uno(npc.Tipo.sonidoDeDisparo, transform, 40f);
        Golpe();
        bolasTiradas++;
        proximaBola = Time.time + EntreBolas;
        if (bolasTiradas >= Bolas) Terminar();
    }

    private void DispararRayo()
    {
        Vector3 desde = OjoPos();
        Vector3 fin = HastaDondeLlega(desde, apuntaA);
        foreach (PlayerMovement jugador in Tocados(desde, fin))
        {
            HealthSystem vida = jugador.GetComponent<HealthSystem>();
            if (vida != null && vida.currentHealth > 0)
            {
                HealthSystem.DamageOrigin = desde;
                HealthSystem.DamageSource = npc.Tipo.nombre;
                vida.TakeDamage(DificultadDeInvasion.DanioDeEnemigo(npc.Tipo.danioADistancia)); // US 161, CA3
                HealthSystem.DamageSource = null;
            }
        }
        BorrarMira();
        Rayo.Mostrar(desde, fin, npc.Tipo.colorADistancia);
        SonidoDeNpc.Uno(npc.Tipo.sonidoDeDisparo, transform, 50f);
        SonidoDeNpc.En(npc.Tipo.sonidoDeImpactoADistancia, fin, 30f);
        Golpe();
        Terminar();
    }

    private void Golpe()
    {
        AnimationClip clip = npc.Tipo.animacionADistancia;
        if (clip == null && npc.Tipo.ataques != null && npc.Tipo.ataques.Length > 0) clip = npc.Tipo.ataques[0];
        if (clip != null && npc.Animaciones != null) npc.Animaciones.Golpe(clip, 0.9f);
    }

    private void Terminar()
    {
        fase = Fase.Libre;
        proximoDisparo = Time.time + npc.Tipo.cadaCuantoDispara * Random.Range(0.85f, 1.15f);
    }

    private void Cancelar()
    {
        if (fase == Fase.Libre) return;
        fase = Fase.Libre;
        BorrarMira();
        proximoDisparo = Time.time + npc.Tipo.cadaCuantoDispara * 0.5f;
    }

    // Hasta dónde llega una línea del ojo hacia ese punto (y más allá): la primera pared. Los jugadores y los bichos no
    // la frenan (a los jugadores los toca Tocados, por el grosor del rayo).
    private Vector3 HastaDondeLlega(Vector3 desde, Vector3 hacia)
    {
        Vector3 dir = hacia - desde;
        if (dir.sqrMagnitude < 1e-4f) return hacia;
        dir.Normalize();
        float mejor = LargoRayo;
        foreach (RaycastHit g in Physics.RaycastAll(desde, dir, LargoRayo, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.distance >= mejor || g.collider.transform.IsChildOf(transform)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null) continue; // los otros bichos no lo frenan
            if (g.collider.GetComponentInParent<PortalDeEnemigos>() != null) continue;
            if (g.collider.GetComponentInParent<PlayerMovement>() != null || g.collider is CharacterController) continue;
            mejor = g.distance;
        }
        return desde + dir * mejor;
    }

    // CA3: los jugadores que toca el rayo (de desde a fin), por su cuerpo (la cápsula del CharacterController) y el grosor
    // del rayo. Así no depende de en qué capa esté su colisión ni de pegarle justo con una línea finita.
    private static List<PlayerMovement> Tocados(Vector3 desde, Vector3 fin)
    {
        var tocados = new List<PlayerMovement>();
        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            CharacterController cc = p.GetComponent<CharacterController>();
            Vector3 abajo, arriba;
            float radio;
            if (cc != null)
            {
                Bounds b = cc.bounds;
                radio = Mathf.Min(b.extents.x, b.extents.z);
                abajo = b.center - Vector3.up * Mathf.Max(0f, b.extents.y - radio);
                arriba = b.center + Vector3.up * Mathf.Max(0f, b.extents.y - radio);
            }
            else { radio = 0.35f; abajo = p.transform.position + Vector3.up * 0.3f; arriba = p.transform.position + Vector3.up * 1.6f; }
            if (DistanciaEntreSegmentos(desde, fin, abajo, arriba) <= radio + RadioDelRayo) tocados.Add(p);
        }
        return tocados;
    }

    // La menor distancia entre los segmentos p1-q1 y p2-q2.
    private static float DistanciaEntreSegmentos(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-6f && e <= 1e-6f) return r.magnitude;
        if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                s = den > 1e-6f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
    }

    private bool LoVeDesde(Vector3 ojos, Transform jugador)
    {
        Vector3 pecho = Pecho(jugador);
        Vector3 dir = pecho - ojos;
        float d = dir.magnitude;
        if (d < 1e-3f) return true;
        foreach (RaycastHit g in Physics.RaycastAll(ojos, dir / d, d, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.transform.IsChildOf(transform) || g.collider.transform.IsChildOf(jugador)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null || g.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            if (g.collider.GetComponentInParent<PortalDeEnemigos>() != null) continue;
            return false;
        }
        return true;
    }

    // ---------- La mira (US 285) ----------

    private void DibujarMira(Vector3 desde, Vector3 hasta, float ancho, Color intensidad)
    {
        if (mira == null)
        {
            var go = new GameObject("Mira del rayo");
            go.transform.SetParent(transform, false);
            mira = go.AddComponent<LineRenderer>();
            mira.positionCount = 2;
            mira.useWorldSpace = true;
            mira.shadowCastingMode = ShadowCastingMode.Off;
            mira.receiveShadows = false;
            mira.numCapVertices = 2;
            materialLinea = Rayo.Material(npc.Tipo.colorADistancia * 1.5f);
            mira.sharedMaterial = materialLinea;
        }
        mira.enabled = true;
        mira.SetPosition(0, desde);
        mira.SetPosition(1, hasta);
        mira.startWidth = mira.endWidth = ancho;
        mira.startColor = mira.endColor = new Color(intensidad.r, intensidad.g, intensidad.b, 1f);
    }

    private void BorrarMira()
    {
        if (mira != null) mira.enabled = false;
    }

    private void OnDisable()
    {
        BorrarMira();
        Soltar();
    }

    private void OnDestroy()
    {
        if (materialLinea != null) Destroy(materialLinea);
    }
}

// US 285: el rayo del ojo, que se ve un instante y se apaga (con un fogonazo donde pega).
public static class Rayo
{
    private const float Dura = 0.35f, Ancho = 0.5f;

    /// <summary>Un material de línea que brilla de ese color (HDR, con el bloom).</summary>
    public static Material Material(Color color)
    {
        Shader shader = Resources.Load<Shader>("Efectos/LineaBrillante");
        if (shader == null) shader = Shader.Find("Efectos/LineaBrillante");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = "Línea que brilla" };
        m.SetColor("_Color", new Color(color.r, color.g, color.b, 1f) * 2.5f);
        m.color = color;
        return m;
    }

    public static void Mostrar(Vector3 desde, Vector3 hasta, Color color)
    {
        var go = new GameObject("Rayo");
        var linea = go.AddComponent<LineRenderer>();
        linea.positionCount = 2;
        linea.useWorldSpace = true;
        linea.SetPosition(0, desde);
        linea.SetPosition(1, hasta);
        linea.shadowCastingMode = ShadowCastingMode.Off;
        linea.receiveShadows = false;
        linea.numCapVertices = 4;
        linea.startWidth = linea.endWidth = Ancho;
        Material m = Material(color);
        linea.sharedMaterial = m;
        go.AddComponent<Apagarse>().Iniciar(linea, m, Dura);
        Destellos.En(hasta, color, 14);
    }

    private class Apagarse : MonoBehaviour
    {
        private LineRenderer linea;
        private Material material;
        private float dura, desde;

        public void Iniciar(LineRenderer l, Material m, float d) { linea = l; material = m; dura = d; desde = Time.time; }

        private void Update()
        {
            float t = (Time.time - desde) / dura;
            if (t >= 1f) { Destroy(gameObject); return; }
            float queda = 1f - t;
            linea.startWidth = linea.endWidth = Ancho * (0.4f + 0.6f * queda);
            linea.startColor = linea.endColor = new Color(queda, queda, queda, 1f);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}

// US 286: una bola de energía de la ráfaga del Watcher verde: va derecho hacia donde apuntó, brilla con su color, le
// hace el daño del tipo al jugador que toca y revienta contra las paredes.
public class BolaDeEnergia : MonoBehaviour
{
    private const float Velocidad = 16f, Radio = 0.18f, Vive = 4f;

    private Vector3 direccion;
    private TipoDeNpc tipo;
    private Transform quien;
    private float muere;

    public static void Lanzar(Vector3 desde, Vector3 hacia, TipoDeNpc tipo, Transform quien)
    {
        Vector3 dir = hacia - desde;
        if (dir.sqrMagnitude < 1e-4f) dir = quien != null ? quien.forward : Vector3.forward;
        var go = new GameObject("Bola de energía");
        go.transform.position = desde;
        var bola = go.AddComponent<BolaDeEnergia>();
        bola.direccion = dir.normalized;
        bola.tipo = tipo;
        bola.quien = quien;
        bola.muere = Time.time + Vive;
        Destellos.Bola(go.transform, tipo.colorADistancia, Radio);
    }

    private void Update()
    {
        Vector3 antes = transform.position;
        float paso = Velocidad * Time.deltaTime;
        RaycastHit? choque = null;
        foreach (RaycastHit g in Physics.SphereCastAll(antes, Radio, direccion, paso, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (quien != null && g.collider.transform.IsChildOf(quien)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null) continue; // los otros bichos no la frenan
            if (choque == null || g.distance < choque.Value.distance) choque = g;
        }
        if (choque.HasValue)
        {
            RaycastHit g = choque.Value;
            PlayerMovement jugador = g.collider.GetComponentInParent<PlayerMovement>();
            if (jugador != null)
            {
                HealthSystem vida = jugador.GetComponent<HealthSystem>();
                if (vida != null && vida.currentHealth > 0)
                {
                    HealthSystem.DamageOrigin = antes;
                    HealthSystem.DamageSource = tipo != null ? tipo.nombre : null;
                    vida.TakeDamage(DificultadDeInvasion.DanioDeEnemigo(tipo != null ? tipo.danioADistancia : 10)); // US 161, CA3
                    HealthSystem.DamageSource = null;
                }
            }
            Vector3 donde = g.distance > 0f ? antes + direccion * g.distance : antes;
            Reventar(donde);
            return;
        }
        transform.position = antes + direccion * paso;
        if (Time.time >= muere) Reventar(transform.position);
    }

    private void Reventar(Vector3 donde)
    {
        if (tipo != null) SonidoDeNpc.En(tipo.sonidoDeImpactoADistancia, donde, 30f);
        Destellos.En(donde, tipo != null ? tipo.colorADistancia : Color.green, 12);
        Destellos.Soltar(gameObject);
        Destroy(gameObject);
    }
}

// Las partículas que brillan de los ataques a distancia (con Efectos/ParticulaBrillante, sumando luz, con el bloom).
public static class Destellos
{
    private static readonly Dictionary<Color, Material> materiales = new Dictionary<Color, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => materiales.Clear();

    private static Material Material(Color color)
    {
        if (materiales.TryGetValue(color, out Material m) && m != null) return m;
        Shader shader = Resources.Load<Shader>("Efectos/ParticulaBrillante");
        if (shader == null) shader = Shader.Find("Efectos/ParticulaBrillante");
        if (shader == null) return null;
        m = new Material(shader) { name = "Destello" };
        m.SetColor("_Color", new Color(color.r, color.g, color.b, 1f) * 2.6f);
        m.SetFloat("_Suave", 0.7f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        materiales[color] = m;
        return m;
    }

    private static ParticleSystem Sistema(string nombre, Transform padre, Color color, bool mundo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false;
        m.simulationSpace = mundo ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        m.startColor = Color.white;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.02f;
        var c = ps.colorOverLifetime;
        c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        c.color = g;
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = Material(color);
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        return ps;
    }

    /// <summary>La bola: un núcleo que late y una estela.</summary>
    public static void Bola(Transform bola, Color color, float radio)
    {
        ParticleSystem nucleo = Sistema("Núcleo", bola, color, false);
        var m = nucleo.main;
        m.loop = true;
        m.startLifetime = 0.25f;
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(radio * 3.5f, radio * 5f);
        m.maxParticles = 20;
        var e = nucleo.emission;
        e.rateOverTime = 40f;
        nucleo.Play();

        ParticleSystem estela = Sistema("Estela", bola, color, true);
        m = estela.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(radio * 1.2f, radio * 2.2f);
        m.maxParticles = 80;
        e = estela.emission;
        e.rateOverDistance = 18f;
        e.rateOverTime = 0f;
        var t = estela.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        estela.Play();
    }

    /// <summary>Suelta la estela antes de borrar la bola, así se apaga sola.</summary>
    public static void Soltar(GameObject bola)
    {
        foreach (ParticleSystem ps in bola.GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(ps.gameObject, 0.6f);
        }
    }

    /// <summary>Un chispazo en ese lugar (donde pega un rayo o revienta una bola).</summary>
    public static void En(Vector3 lugar, Color color, int chispas)
    {
        var go = new GameObject("Destello");
        go.transform.position = lugar;
        ParticleSystem flash = Sistema("Fogonazo", go.transform, color, true);
        var m = flash.main;
        m.loop = false;
        m.duration = 0.1f;
        m.startLifetime = 0.18f;
        m.startSpeed = 0f;
        m.startSize = 1.2f;
        m.maxParticles = 1;
        var e = flash.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem chis = Sistema("Chispas", go.transform, color, true);
        m = chis.main;
        m.loop = false;
        m.duration = 0.1f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        m.gravityModifier = 0.8f;
        m.maxParticles = chispas;
        e = chis.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)chispas) });

        flash.Play();
        chis.Play();
        Object.Destroy(go, 1f);
    }
}
