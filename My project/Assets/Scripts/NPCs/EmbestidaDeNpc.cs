using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// F11 (NPCs), US 266: la embestida del jefe (los tipos con TipoDeNpc.embiste).
// - CA2: si el jugador está entre 3 m y 30 m (entre los cuerpos) y lo ve, cada 10 s se prepara para embestir: se
//   frena mirándolo, baja el cuerpo hacia el piso y aparece una línea roja, un poco más ancha que el jefe, que marca el
//   camino: en línea recta hacia donde está el jugador y de largo, 30 m o hasta la primera pared (o el borde del
//   NavMesh), aunque el jugador esté más cerca. La línea se ve 1,5 s antes.
// - CA3: al terminar el aviso se lanza exactamente por la línea, a 15 m/s, sin corregir hacia el jugador. El jugador que
//   está sobre la línea cuando pasa recibe 40 de daño (primero el escudo) y sale empujado hacia el costado.
// - CA4: si el jugador salió de la línea antes de que pase, no le hace nada.
// - CA5: al terminar queda quieto 1,5 s recuperándose y vuelve a perseguir.
// - CA6: se mueve por el NavMesh (no atraviesa paredes) y frena si choca con algo.
// Mientras tanto la persecución lo deja quieto (PersecucionDeNpc.Ocupado) y no lo gira (Controlado), y el golpe
// cuerpo a cuerpo no arranca. La flash o la muerte la cortan. CadaCuanto lo cambia la furia (US 268).
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class EmbestidaDeNpc : MonoBehaviour
{
    private const float DesdeLejos = 3f, HastaLejos = 30f; // CA2: m entre los cuerpos
    private const float Aviso = 1.5f;                    // CA2: s con la línea antes de arrancar
    private const float Largo = 30f, Minimo = 3f;         // CA2: m del camino, como mucho (si es más corto que Minimo, no embiste)
    private const float MasAncho = 1.4f;                  // CA2 y CA3: la línea y el golpe, más anchos que el cuerpo
    private const float Bajar = 1.2f, BajarEn = 0.4f;     // m que baja el cuerpo al embestir y en cuántos s
    private const float Velocidad = 15f;                  // CA3: m/s
    private const int Danio = 40;                         // CA3
    private const float Empujon = 3.5f, EmpujonDura = 0.3f; // CA3: m hacia el costado y en cuántos s
    private const float Recupera = 1.5f;                  // CA5: s quieto después
    private const float PrimeraVez = 4f;                  // s desde que aparece hasta la primera
    private const float AnimacionMasRapida = 2.2f;        // la animación de embestir, acelerada (acompaña la velocidad)

    /// <summary>Segundos entre embestidas (CA2). La furia (US 268) lo baja a 6.</summary>
    public float CadaCuanto = 10f;

    /// <summary>La furia (US 268): embiste más seguido desde ahora.</summary>
    public void MasSeguido(float cadaCuanto)
    {
        CadaCuanto = cadaCuanto;
        proxima = Mathf.Min(proxima, Time.time + cadaCuanto);
    }

    /// <summary>Está embistiendo (preparando, lanzado o recuperándose).</summary>
    public bool Embistiendo => fase != Fase.Libre;

    private enum Fase { Libre, Aviso, Lanzado, Recupera }

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private Fase fase = Fase.Libre;
    private float proxima, faseHasta, largo, recorrido;
    private Vector3 direccion, desde;
    private GameObject linea;
    private Material materialLinea;
    private readonly List<HealthSystem> golpeados = new List<HealthSystem>();
    private readonly List<HealthSystem> jugadores = new List<HealthSystem>();
    private float bajado;          // m que tiene bajado el cuerpo ahora
    private float alturaModelo;    // la altura normal del modelo (local)

    private void Awake()
    {
        npc = GetComponent<Npc>();
        persecucion = GetComponent<PersecucionDeNpc>();
        proxima = Time.time + PrimeraVez;
        if (npc.Modelo != null) alturaModelo = npc.Modelo.localPosition.y;
    }

    private void OnDisable() => Cortar(false);
    private void OnDestroy() => Cortar(false);

    private void Update()
    {
        if (persecucion == null) persecucion = GetComponent<PersecucionDeNpc>();
        if (npc == null || npc.Tipo == null || persecucion == null) return;
        if (!npc.Vivo) { Cortar(false); enabled = false; return; }
        if (npc.Aturdido) { if (fase != Fase.Libre) Cortar(false); return; } // la flash la corta

        Agacharse(fase == Fase.Aviso || fase == Fase.Lanzado);

        switch (fase)
        {
            case Fase.Libre:
                if (Time.time < proxima) break;
                if (persecucion.Ocupado) { Contar("está ocupado (golpeando)"); break; }
                if (PuedeEmbestir(out Vector3 hacia, out float cuanto)) Preparar(hacia, cuanto);
                break;
            case Fase.Aviso:
                Pintar(0.35f + 0.25f * Mathf.PingPong(Time.time * 4f, 1f));
                if (Time.time >= faseHasta) Lanzar();
                break;
            case Fase.Lanzado:
                Avanzar();
                break;
            case Fase.Recupera:
                if (Time.time >= faseHasta) Cortar(true);
                break;
        }
    }

    // CA2: el jugador a entre 5 y 15 m, a la vista, y lugar para lanzarse.
    private bool PuedeEmbestir(out Vector3 hacia, out float cuanto)
    {
        hacia = Vector3.zero;
        cuanto = 0f;
        Transform jugador = persecucion.Objetivo;
        if (jugador == null) { Contar("no encuentra al jugador"); return false; }
        if (persecucion.Agente == null) { Contar("no está sobre el NavMesh"); return false; }
        float entre = persecucion.EntreCuerpos(jugador);
        if (entre < DesdeLejos || entre > HastaLejos)
        {
            Vector3 d = jugador.position - transform.position; d.y = 0f;
            Contar($"estás a {entre:0.0} m entre los cuerpos ({d.magnitude:0.0} m entre los centros; su frente sale {npc.Frente:0.0} m), fuera de {DesdeLejos}–{HastaLejos} m");
            return false;
        }
        if (!Ve(jugador)) { Contar($"no te ve (estás a {entre:0.0} m)"); return false; }
        hacia = jugador.position - transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude < 1e-4f) return false;
        hacia.Normalize();
        cuanto = Camino(hacia); // de largo, hasta el final o una pared
        if (cuanto < Minimo) { Contar($"no tiene lugar para lanzarse ({cuanto:0.0} m libres, necesita {Minimo})"); return false; }
        return true;
    }

    // Por qué no embiste (cada 2 s como mucho), para ajustarla: solo si el proyecto define DEPURAR_NPCS (Player
    // Settings > Scripting Define Symbols). Si no, estas llamadas ni se compilan.
    private float contadoEn = -10f;
    [System.Diagnostics.Conditional("DEPURAR_NPCS")]
    private void Contar(string porQue)
    {
        if (Time.time - contadoEn < 2f) return;
        contadoEn = Time.time;
        Debug.Log($"[Embestida] {npc.Tipo.nombre} no embiste: {porQue}.");
    }

    private bool Ve(Transform jugador)
    {
        Vector3 ojos = transform.position + Vector3.up * 1.5f;
        Vector3 pecho = jugador.position + Vector3.up * 0.8f;
        foreach (RaycastHit g in Physics.RaycastAll(ojos, (pecho - ojos).normalized, Vector3.Distance(ojos, pecho), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.transform.IsChildOf(transform) || g.collider.transform.IsChildOf(jugador)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null || g.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            return false;
        }
        return true;
    }

    // CA2 y CA6: hasta dónde puede ir derecho: 15 m, o hasta el borde del NavMesh o una pared (a la altura del cuerpo).
    private float Camino(Vector3 hacia)
    {
        float cuanto = Largo;
        NavMeshAgent agente = persecucion.Agente;
        var filtro = new NavMeshQueryFilter { agentTypeID = agente.agentTypeID, areaMask = NavMesh.AllAreas };
        Vector3 inicio = agente.nextPosition; // sobre el NavMesh
        if (NavMesh.Raycast(inicio, inicio + hacia * Largo, out NavMeshHit borde, filtro))
            cuanto = Mathf.Min(cuanto, borde.distance - 0.2f);
        // Paredes a la altura del cuerpo (sin contar el piso ni las rampas: lo que mira para arriba).
        float radio = Mathf.Clamp(npc.Ancho * 0.5f, 0.2f, 0.8f);
        Vector3 cuerpo = transform.position + Vector3.up * (radio + 0.5f);
        foreach (RaycastHit g in Physics.SphereCastAll(cuerpo, radio, hacia, Largo + npc.Frente, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.transform.IsChildOf(transform)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null || g.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            if (g.distance <= 0f || g.normal.y > 0.5f) continue; // ya lo tocaba, o es piso o rampa
            cuanto = Mathf.Min(cuanto, g.distance - npc.Frente);
        }
        return cuanto;
    }

    private void Preparar(Vector3 hacia, float cuanto)
    {
        fase = Fase.Aviso;
        faseHasta = Time.time + Aviso;
        direccion = hacia;
        largo = cuanto;
        persecucion.Ocupado = true;
        persecucion.Controlado = true;
        transform.rotation = Quaternion.LookRotation(direccion, Vector3.up);
        Animar(npc.Tipo.preparaEmbestida != null ? npc.Tipo.preparaEmbestida : npc.Tipo.quietoEnCombate, false, 1f);
        Dibujar();
    }

    private void Lanzar()
    {
        fase = Fase.Lanzado;
        desde = transform.position;
        recorrido = 0f;
        golpeados.Clear();
        jugadores.Clear();
        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            HealthSystem v = p.GetComponent<HealthSystem>();
            if (v != null) jugadores.Add(v);
        }
        Pintar(0.55f);
        AnimationClip clip = npc.Tipo.embestida != null ? npc.Tipo.embestida : npc.Tipo.correr != null ? npc.Tipo.correr : npc.Tipo.caminar;
        Animar(clip, true, AnimacionMasRapida);
    }

    // CA3 y CA6: por la línea, por el NavMesh; frena al llegar al final o si se trabó contra algo.
    private void Avanzar()
    {
        NavMeshAgent agente = persecucion.Agente;
        if (agente == null) { Recuperarse(); return; }
        float paso = Mathf.Min(Velocidad * Time.deltaTime, largo - recorrido);
        Vector3 antes = transform.position;
        agente.Move(direccion * paso);
        Vector3 movido = transform.position - antes;
        movido.y = 0f;
        recorrido += movido.magnitude;
        transform.rotation = Quaternion.LookRotation(direccion, Vector3.up);
        Golpear();
        bool trabado = paso > 0.01f && movido.magnitude < paso * 0.3f;
        if (recorrido >= largo - 0.05f || trabado) Recuperarse();
    }

    // CA3 y CA4: al que está adelante del cuerpo, sobre la línea, una sola vez por embestida.
    private void Golpear()
    {
        foreach (HealthSystem vida in jugadores)
        {
            if (vida == null || vida.currentHealth <= 0 || golpeados.Contains(vida)) continue;
            float radio = PersecucionDeNpc.RadioDe(vida.transform);
            Vector3 rel = vida.transform.position - transform.position;
            rel.y = 0f;
            float adelante = Vector3.Dot(rel, direccion);
            float costado = Vector3.Cross(direccion, rel).y; // + a la derecha, - a la izquierda
            if (adelante < -npc.Frente * 0.5f || adelante > npc.Frente + radio || Mathf.Abs(costado) > npc.Ancho * MasAncho + radio) continue;

            golpeados.Add(vida);
            HealthSystem.DamageOrigin = transform.position + Vector3.up;
            HealthSystem.DamageSource = npc.Tipo.nombre;
            vida.TakeDamage(DificultadDeInvasion.DanioDeEnemigo(Danio)); // US 161, CA3
            HealthSystem.DamageSource = null;
            Vector3 lado = Vector3.Cross(Vector3.up, direccion) * (costado >= 0f ? 1f : -1f);
            EmpujonAlJugador.Empujar(vida.transform, (lado + direccion * 0.35f).normalized * Empujon, EmpujonDura);
        }
    }

    private void Recuperarse()
    {
        fase = Fase.Recupera;
        faseHasta = Time.time + Recupera;
        Borrar();
        Animar(npc.Tipo.quietoEnCombate != null ? npc.Tipo.quietoEnCombate : npc.Tipo.quieto, true, 1f);
    }

    // Vuelve a perseguir (normal) o se corta (flash, muerte).
    private void Cortar(bool normal)
    {
        if (fase != Fase.Libre && persecucion != null)
        {
            persecucion.Ocupado = false;
            persecucion.Controlado = false;
        }
        if (fase != Fase.Libre || !normal) proxima = Time.time + CadaCuanto;
        fase = Fase.Libre;
        Borrar();
        if (!normal && npc != null && npc.Modelo != null && bajado > 0f && npc.Vivo)
        {
            bajado = 0f;
            Vector3 p = npc.Modelo.localPosition;
            p.y = alturaModelo;
            npc.Modelo.localPosition = p;
        }
    }

    // CA2: baja el cuerpo al prepararse y mientras embiste; después vuelve a su altura.
    private void Agacharse(bool abajo)
    {
        if (npc.Modelo == null) return;
        float meta = abajo ? Bajar : 0f;
        if (Mathf.Approximately(bajado, meta)) return;
        bajado = Mathf.MoveTowards(bajado, meta, Bajar / BajarEn * Time.deltaTime);
        Vector3 p = npc.Modelo.localPosition;
        p.y = alturaModelo - bajado;
        npc.Modelo.localPosition = p;
    }

    private void Animar(AnimationClip clip, bool loop, float velocidad)
    {
        if (clip == null || npc.Animaciones == null) return;
        npc.Animaciones.Poner(clip, loop, 0.15f, true);
        npc.Animaciones.Velocidad(velocidad);
    }

    // ---------- La línea roja (CA2) ----------

    private void Dibujar()
    {
        Borrar();
        linea = GameObject.CreatePrimitive(PrimitiveType.Quad);
        linea.name = "Embestida (camino)";
        Collider c = linea.GetComponent<Collider>();
        if (c != null) Destroy(c);
        float total = largo + npc.Frente;
        float ancho = Mathf.Max(1f, npc.Ancho * 2f * MasAncho);
        Vector3 piso = transform.position;
        if (Physics.Raycast(piso + Vector3.up * 1f, Vector3.down, out RaycastHit g, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            piso.y = g.point.y;
        linea.transform.position = piso + direccion * (total * 0.5f) + Vector3.up * 0.05f;
        linea.transform.rotation = Quaternion.LookRotation(Vector3.down, direccion); // acostada, a lo largo del camino
        linea.transform.localScale = new Vector3(ancho, total, 1f);
        var render = linea.GetComponent<MeshRenderer>();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        if (materialLinea == null)
        {
            Shader shader = Shader.Find("Sprites/Default"); // siempre está en el build
            materialLinea = new Material(shader) { name = "Embestida (camino)", renderQueue = 3000 };
        }
        render.sharedMaterial = materialLinea;
        Pintar(0.35f);
    }

    private void Pintar(float alfa)
    {
        if (materialLinea != null) materialLinea.color = new Color(1f, 0.15f, 0.12f, alfa);
    }

    private void Borrar()
    {
        if (linea != null) Destroy(linea);
        linea = null;
    }
}

// El empujón de la embestida (US 266, CA3): mueve al jugador un poco, en unos instantes, con su CharacterController
// (así no atraviesa paredes). Solo al que se maneja en esta computadora.
public class EmpujonAlJugador : MonoBehaviour
{
    private CharacterController cuerpo;
    private Vector3 velocidad;
    private float hasta;

    public static void Empujar(Transform jugador, Vector3 cuanto, float segundos)
    {
        CharacterController cc = jugador != null ? jugador.GetComponent<CharacterController>() : null;
        PlayerMovement mov = jugador != null ? jugador.GetComponent<PlayerMovement>() : null;
        if (cc == null || mov == null || !mov.enabled) return;
        EmpujonAlJugador e = jugador.GetComponent<EmpujonAlJugador>();
        if (e == null) e = jugador.gameObject.AddComponent<EmpujonAlJugador>();
        e.cuerpo = cc;
        e.velocidad = cuanto / Mathf.Max(0.05f, segundos);
        e.hasta = Time.time + segundos;
        e.enabled = true;
    }

    private void Update()
    {
        if (cuerpo == null || Time.time >= hasta || !cuerpo.enabled) { enabled = false; return; }
        float queda = Mathf.Clamp01((hasta - Time.time) / 0.3f);
        cuerpo.Move(velocidad * (0.4f + 0.6f * queda) * Time.deltaTime);
    }
}
