using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Herramienta para testear armas y granadas (solo para escenas de prueba). Ya está armada en Scenes/Pruebas_Armas
// (objeto "Pista de pruebas"): abrir la escena y darle Play. Para usarla en otra escena: un objeto vacío con este
// componente y el prefab MuniecoPrueba en "Munieco". Todo se configura en el Inspector.
// - Pista: sale desde este objeto hacia adelante (su eje azul), marcada cada 1 m (cada 5 m, en amarillo) con el número
//   de metros al costado.
// - Muñecos que hacen de jugador (el modelo del jugador, quietos, con contorno rojo de rival), con 100 de vida y 25 de
//   escudo. Arriba de cada uno se ve su vida, su escudo y el daño que recibió (último golpe y total).
//   * "Filas": una fila por distancia (25 y 35 m), con un muñeco para tirarle a las piernas, otro al cuerpo y otro a
//     la cabeza (US 165: daño por zona y por distancia).
//   * "Distancias": muñecos sueltos sobre la pista. "Otros": fuera de la pista (por ejemplo, detrás de una pared).
//     "Companeros": hacen de compañero (marca azul, sin contorno).
//   * Inicio: todos a 100 de vida, 25 de escudo y el daño en 0 (solos, 3 s después de morir). Fin: cambian hacia
//     dónde miran (de frente, de costado o de espaldas).
// - Granada flash (US 075): con "Mostrar Flash", arriba de cada muñeco y en el cuadro de arriba a la izquierda se ve
//   qué le habría hecho a un jugador en su lugar, con las mismas reglas que FlashBlind (distancia, ángulo, paredes).
// - Muñeco que dispara La Porteña cada 1 s, con el mismo sonido 3D que los disparos de los demás jugadores en el
//   online (de 3 a 80 m): su cartel dice a cuántos metros estás y el volumen que te llega. Supr lo pausa.
public class PistaDeDistancias : MonoBehaviour
{
    [Tooltip("Prendido: los carteles y el cuadro muestran la distancia, hacia dónde miran y el resultado de la flash. " +
             "Apagado: los muñecos muestran solo vida y escudo (para probar armas).")]
    public bool mostrarFlash = false;
    [Tooltip("Largo de la pista, en metros.")]
    public int largo = 35;
    [Tooltip("Muñeco que hace de jugador (MuniecoPrueba). Se le saca la persecución: queda quieto.")]
    public GameObject munieco;
    [Tooltip("A cuántos metros del 0 va cada muñeco.")]
    public int[] distancias = { };
    [Tooltip("Filas de muñecos para probar el daño de un arma: una fila por distancia (en metros), cada una con un " +
             "muñeco por zona, uno al lado del otro (piernas, cuerpo y cabeza: a cada uno se le tira a esa zona).")]
    public int[] filas = { 25, 35 };
    public string[] zonasFila = { "piernas", "cuerpo", "cabeza" };
    [Tooltip("Metros entre los muñecos de una misma fila.")]
    public float separacionFila = 1.5f;
    [Header("Muñeco que dispara (sonido de cerca y de lejos)")]
    [Tooltip("Un muñeco que dispara La Porteña cada tanto, con el mismo sonido 3D con el que se escuchan los disparos " +
             "de los demás jugadores en el online (JugadorEnRed: de 3 m a 80 m, baja en línea recta). Supr lo pausa.")]
    public bool tirador = true;
    [Tooltip("Dónde está, en metros desde el 0 de la pista (x: a la derecha, z: hacia adelante).")]
    public Vector3 lugarTirador = new Vector3(0f, 0f, 30f);
    [Tooltip("Segundos entre disparo y disparo.")]
    public float cadenciaTirador = 1f;
    private const float SonidoMin = 3f, SonidoMax = 80f; // los mismos que JugadorEnRed
    private Transform tiradorT;
    private TextMeshPro cartelTirador;
    private AudioSource sonidoTirador;
    private AudioClip disparo;
    private float proximoDisparo;
    private bool tiradorPausado;

    [Tooltip("Más muñecos fuera de la pista, en metros desde el 0 de la pista (x: a la derecha, z: hacia adelante). " +
             "Miran hacia la pista. Por ejemplo, uno detrás de la pared roja.")]
    public Vector3[] otros = { };
    [Tooltip("Nombre de cada uno de \"otros\", para el cartel.")]
    public string[] nombresOtros = { };
    [Tooltip("Muñecos que hacen de compañero (sin contorno rojo, con la marca azul arriba, US 193), en metros desde el 0 " +
             "de la pista. La flash también los enceguece (US 075, CA6).")]
    public Vector3[] companeros = { };
    [Tooltip("Escudo con el que empieza cada muñeco (vida: la de su HealthSystem, 100).")]
    public int escudo = 25;

    private class Munieco
    {
        public int metros;
        public bool fuera;          // fuera de la pista (de "otros" o "companeros")
        public bool companero;
        public bool deFila;         // de "filas": miran hacia el 0
        public int ultimoGolpe, dano, golpes;
        public Vector3 lugar;       // de "otros": dónde, desde el 0
        public string nombre;
        public GameObject marca;    // la del compañero
        public Transform t;
        public HealthSystem vida;
        public TextMeshPro cartel;
        public string flash = "";
        public float muertoDesde = -1f;
        public float pies; // de su centro a los pies (negativo)
        public Vector3 Pies => t.position + Vector3.up * pies;
    }
    private readonly System.Collections.Generic.List<Munieco> munecos = new System.Collections.Generic.List<Munieco>();

    private enum Mirada { DeFrente, DeCostado, DeEspaldas }
    private Mirada mirada = Mirada.DeFrente;
    private string ultima = "Tirá una flash.";
    private Material blanco, amarillo, gris;

    private void Start()
    {
        blanco = CrearMaterial(new Color(0.95f, 0.95f, 0.95f));
        amarillo = CrearMaterial(new Color(1f, 0.82f, 0.1f));
        gris = CrearMaterial(new Color(0.18f, 0.18f, 0.2f));
        ArmarPista();
        if (munieco != null)
        {
            foreach (int metros in distancias) Crear(new Munieco { metros = metros, nombre = $"{metros} m" });
            for (int i = 0; otros != null && i < otros.Length; i++)
            {
                string nombre = nombresOtros != null && i < nombresOtros.Length ? nombresOtros[i] : $"otro {i + 1}";
                Crear(new Munieco { fuera = true, lugar = otros[i], nombre = nombre });
            }
            for (int f = 0; filas != null && f < filas.Length; f++)
                for (int i = 0; zonasFila != null && i < zonasFila.Length; i++)
                {
                    float x = (i - (zonasFila.Length - 1) * 0.5f) * separacionFila;
                    Crear(new Munieco { fuera = true, deFila = true, lugar = new Vector3(x, 0f, filas[f]), metros = filas[f],
                                        nombre = $"{filas[f]} m · {zonasFila[i]}" });
                }
            for (int i = 0; companeros != null && i < companeros.Length; i++)
                Crear(new Munieco { fuera = true, companero = true, lugar = companeros[i],
                                    nombre = companeros.Length > 1 ? $"compañero {i + 1}" : "compañero" });
        }
        UbicarMunecos();
        if (tirador && munieco != null) CrearTirador();
        StartCoroutine(PonerEscudo()); // HealthSystem lo deja en 0 en su Start: se pone después
    }

    private void Crear(Munieco mu)
    {
        GameObject m = Instantiate(munieco, transform);
        m.name = "Muñeco " + mu.nombre;
        // Se le saca la persecución, pero antes se toma su material del contorno rojo de rival.
        Material contorno = null;
        foreach (EnemyChaser perseguir in m.GetComponentsInChildren<EnemyChaser>(true))
        {
            if (contorno == null) contorno = perseguir.outline;
            Destroy(perseguir);
        }
        if (contorno == null && ConfigRed.Actual != null) contorno = ConfigRed.Actual.contornoRival;
        foreach (Rigidbody rb in m.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        mu.t = m.transform;
        mu.vida = m.GetComponent<HealthSystem>();
        // Contorno rojo de enemigo (como los rivales en el online, US 031), mientras está vivo.
        Animator animador = m.GetComponentInChildren<Animator>();
        HealthSystem vida = mu.vida;
        mu.cartel = Texto(transform, "", Vector3.zero, mu.deFila ? 1f : 0.7f, Color.white);
        if (mu.companero) Marca(mu); // los compañeros no tienen contorno
        else if (animador != null && contorno != null)
            ContornoRival.Crear(m, animador.transform, contorno, () => vida == null || vida.currentHealth > 0);
        munecos.Add(mu);
    }

    // Compañero: el rombo azul del equipo arriba de la cabeza (como MarcasDeCompaneros, #58A6FF).
    private static readonly Color Azul = new Color(0.345f, 0.651f, 1f);
    private void Marca(Munieco mu)
    {
        GameObject rombo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(rombo.GetComponent<Collider>());
        rombo.name = "Marca de compañero";
        rombo.transform.SetParent(mu.cartel.transform, false);
        rombo.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        rombo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        rombo.transform.localScale = new Vector3(0.22f, 0.22f, 0.02f);
        Renderer r = rombo.GetComponent<Renderer>();
        r.sharedMaterial = CrearMaterial(Azul);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mu.marca = rombo;
    }

    // El muñeco que dispara: suena igual que un jugador de la red disparando (mismo AudioSource que JugadorEnRed).
    private void CrearTirador()
    {
        Pistola pistola = FindAnyObjectByType<Pistola>(FindObjectsInactive.Include);
        disparo = pistola != null ? pistola.shootSound : null;
        if (disparo == null) { Debug.LogWarning("[Pista] No encontré el sonido de La Porteña (Pistola.shootSound)."); return; }

        GameObject m = Instantiate(munieco, transform);
        m.name = "Muñeco que dispara";
        foreach (EnemyChaser perseguir in m.GetComponentsInChildren<EnemyChaser>(true)) Destroy(perseguir);
        foreach (Rigidbody rb in m.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        tiradorT = m.transform;
        CapsuleCollider capsula = tiradorT.GetComponent<CapsuleCollider>();
        float pies = capsula != null ? (capsula.center.y - capsula.height * 0.5f) * tiradorT.lossyScale.y : 0f;
        tiradorT.SetPositionAndRotation(transform.TransformPoint(lugarTirador) - Vector3.up * pies,
            Quaternion.LookRotation(-transform.forward, Vector3.up));

        sonidoTirador = m.AddComponent<AudioSource>();
        sonidoTirador.playOnAwake = false;
        sonidoTirador.spatialBlend = 1f;
        sonidoTirador.minDistance = SonidoMin;
        sonidoTirador.maxDistance = SonidoMax;
        sonidoTirador.rolloffMode = AudioRolloffMode.Linear;
        sonidoTirador.outputAudioMixerGroup = pistola.sfxGroup;

        cartelTirador = Texto(transform, "", Vector3.zero, 1f, new Color(1f, 0.82f, 0.1f));
        cartelTirador.transform.position = tiradorT.position + Vector3.up * (pies + 2.6f);
        proximoDisparo = Time.time + 1f;
    }

    private void ActualizarTirador()
    {
        if (tiradorT == null || sonidoTirador == null) return;
        Keyboard k = Keyboard.current;
        if (k != null && k.deleteKey.wasPressedThisFrame) tiradorPausado = !tiradorPausado;
        if (!tiradorPausado && Time.time >= proximoDisparo)
        {
            proximoDisparo = Time.time + Mathf.Max(0.1f, cadenciaTirador);
            sonidoTirador.PlayOneShot(disparo);
            Trazadora.Fogonazo(tiradorT.position + tiradorT.forward * 0.6f + Vector3.up * 0.4f);
        }
        Camera cam = Camera.main;
        if (cam == null || cartelTirador == null) return;
        float d = Vector3.Distance(cam.transform.position, tiradorT.position);
        float volumen = Mathf.Clamp01(1f - (d - SonidoMin) / (SonidoMax - SonidoMin));
        cartelTirador.text = $"Dispara La Porteña{(tiradorPausado ? " (en pausa)" : "")}\nEstás a {d:0.0} m · volumen {volumen * 100f:0} %";
        cartelTirador.transform.rotation = Quaternion.LookRotation(cartelTirador.transform.position - cam.transform.position);
    }

    private System.Collections.IEnumerator PonerEscudo()
    {
        yield return null;
        foreach (Munieco m in munecos) Revivir(m);
    }

    private void Revivir(Munieco m)
    {
        if (m.vida == null) return;
        m.vida.currentHealth = m.vida.maxHealth;
        m.vida.SetShield(escudo);
        m.muertoDesde = -1f;
        m.flash = "";
        m.ultimoGolpe = m.dano = m.golpes = 0;
    }

    private void OnEnable()
    {
        Grenade1.Exploded += AlExplotar;
        HealthSystem.Damaged += AlRecibirDano;
    }

    private void OnDisable()
    {
        Grenade1.Exploded -= AlExplotar;
        HealthSystem.Damaged -= AlRecibirDano;
    }

    // Daño que le hicieron a cada muñeco: el último golpe (antes de restar el escudo) y el total.
    private void AlRecibirDano(HealthSystem quien, int dano, Vector3? desde)
    {
        foreach (Munieco m in munecos)
            if (m.vida == quien)
            {
                m.ultimoGolpe = dano;
                m.dano += dano;
                m.golpes++;
                Debug.Log($"[Pista] {m.nombre}: golpe de {dano} (total {m.dano} en {m.golpes} golpes)");
            }
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.endKey.wasPressedThisFrame) { mirada = (Mirada)(((int)mirada + 1) % 3); UbicarMunecos(); }
        if (k != null && k.homeKey.wasPressedThisFrame) foreach (Munieco m in munecos) Revivir(m);

        ActualizarTirador();
        Camera cam = Camera.main;
        foreach (Munieco m in munecos)
        {
            if (m.t == null) continue;
            if (m.vida != null && m.vida.currentHealth <= 0)
            {
                if (m.muertoDesde < 0f) m.muertoDesde = Time.time;
                else if (Time.time - m.muertoDesde > 3f) { int u = m.ultimoGolpe, d = m.dano, g = m.golpes; Revivir(m); m.ultimoGolpe = u; m.dano = d; m.golpes = g; }
            }
            string vida = m.vida != null
                ? (m.vida.currentHealth > 0 ? $"Vida {m.vida.currentHealth} · Escudo {m.vida.currentShield}" : "Muerto (vuelve en 3 s)")
                : "";
            string golpes = m.golpes > 0 ? $"\nÚltimo golpe: {m.ultimoGolpe} · Total: {m.dano} ({m.golpes})" : "\nSin golpes";
            if (!mostrarFlash) m.cartel.text = (m.deFila ? m.nombre + "\n" : "") + vida + golpes;
            else m.cartel.text = $"{m.nombre} · {Nombre(mirada)}\n{vida}" + (m.flash.Length > 0 ? "\n" + m.flash : "");
            bool muerto = m.vida != null && m.vida.currentHealth <= 0;
            m.cartel.color = muerto ? new Color(1f, 0.4f, 0.4f) : m.companero ? Azul : Color.white;
            if (m.marca != null && m.marca.activeSelf == muerto) m.marca.SetActive(!muerto);
            if (cam != null) m.cartel.transform.rotation = Quaternion.LookRotation(m.cartel.transform.position - cam.transform.position);
        }
    }

    private void UbicarMunecos()
    {
        foreach (Munieco m in munecos)
        {
            // De frente: los de la pista miran hacia el 0; los de afuera, hacia la pista.
            Vector3 frente = m.fuera && !m.companero && !m.deFila ? (m.lugar.x < 0f ? transform.right : -transform.right) : -transform.forward;
            Vector3 mira = mirada == Mirada.DeFrente ? frente
                         : mirada == Mirada.DeCostado ? Quaternion.Euler(0f, 90f, 0f) * frente
                         : -frente;
            Vector3 lugar = m.fuera ? transform.TransformPoint(m.lugar) : transform.position + transform.forward * m.metros;
            m.t.SetPositionAndRotation(lugar, Quaternion.LookRotation(mira, Vector3.up));
            // Parado sobre la pista: el centro del muñeco está a media altura (su cápsula), no en los pies.
            CapsuleCollider capsula = m.t.GetComponent<CapsuleCollider>();
            m.pies = capsula != null ? (capsula.center.y - capsula.height * 0.5f) * m.t.lossyScale.y : 0f;
            m.t.position -= Vector3.up * m.pies;
            m.cartel.transform.position = m.Pies + Vector3.up * 2.5f;
        }
    }

    private static string Nombre(Mirada m) => m == Mirada.DeFrente ? "de frente" : m == Mirada.DeCostado ? "de costado" : "de espaldas";

    // ---------- La flash ----------

    private void AlExplotar(Grenade1 granada, Vector3 centro)
    {
        ShopItem ficha = granada != null ? granada.Item : null;
        if (ficha == null || ficha.grenadeType != GrenadeType.Flash) return;
        float alcance = ficha.effectRadius > 0f ? ficha.effectRadius : FlashBlind.DefaultRange;
        float total = ficha.effectDuration > 0f ? ficha.effectDuration : 2.5f;

        string paraMunecos = "";
        foreach (Munieco m in munecos)
        {
            if (m.t == null) continue;
            Vector3 ojo = m.Pies + Vector3.up * 1.6f; // a la altura de los ojos de un jugador
            m.flash = "Flash: " + Resultado(ojo, m.t.forward, centro, alcance, total, ficha.recovery, m.t);
            paraMunecos += $"\n  {m.nombre}: {m.flash.Substring(7)}";
        }
        float aLo = Vector3.Dot(centro - transform.position, transform.forward);
        string vos = "";
        if (Camera.main != null)
        {
            Transform c = Camera.main.transform;
            vos = Resultado(c.position, c.forward, centro, alcance, total, ficha.recovery, c.root);
        }
        ultima = $"Flash a {aLo:0.0} m del 0 (alcance {alcance:0} m)\nVos: {vos}\nMuñecos:{paraMunecos}";
        Debug.Log("[Pista] " + ultima.Replace("\n", " | "));
    }

    // Las mismas reglas que FlashBlind.Apply, para alguien que mira desde "ojo" hacia "adelante".
    private static string Resultado(Vector3 ojo, Vector3 adelante, Vector3 centro, float alcance, float total, float vuelta, Transform quien)
    {
        Vector3 hacia = centro - ojo;
        float distancia = hacia.magnitude;
        if (distancia > alcance) return $"{distancia:0.0} m: nada (más lejos que el alcance)";
        if (Tapado(centro, ojo, quien)) return $"{distancia:0.0} m: nada (hay una pared en el medio)";
        float angulo = Vector3.Angle(adelante, hacia);
        if (angulo <= FlashBlind.FrontAngle) return $"{distancia:0.0} m, {angulo:0}°: ciego {total:0.##} s + {vuelta:0.##} s para volver";
        if (angulo <= FlashBlind.SideAngle) return $"{distancia:0.0} m, {angulo:0}°: de costado, ciego {total * 0.5f:0.##} s + {vuelta * 0.5f:0.##} s";
        return $"{distancia:0.0} m, {angulo:0}°: de espaldas, solo se aclara 0,5 s";
    }

    private static bool Tapado(Vector3 desde, Vector3 hasta, Transform quien)
    {
        Vector3 d = hasta - desde;
        float largoRayo = d.magnitude - 0.1f;
        if (largoRayo <= 0f) return false;
        foreach (RaycastHit h in Physics.RaycastAll(desde, d.normalized, largoRayo, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<HealthSystem>() != null) continue;
            if (h.collider.GetComponentInParent<Grenade1>() != null) continue;
            if (h.collider.GetComponentInParent<ArmaEnPiso>() != null) continue;
            if (quien != null && h.collider.transform.IsChildOf(quien.root)) continue;
            if (h.collider.GetComponentInParent<PistaDeDistancias>() != null) continue; // la pista no tapa
            return true;
        }
        return false;
    }

    private void OnGUI()
    {
        var estilo = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, wordWrap = true };
        if (!mostrarFlash)
        {
            string distancia = Camera.main != null
                ? $"{Vector3.Dot(Camera.main.transform.position - transform.position, transform.forward):0.0} m" : "-";
            GUI.Box(new Rect(10, 10, 760, 28), $"Vos: {distancia} del 0 · Inicio: muñecos a 100 de vida, {escudo} de escudo y daño en 0" +
                (tiradorT != null ? " · Supr: pausar al que dispara" : ""), estilo);
            return;
        }
        string vos = Camera.main != null
            ? $"{Vector3.Dot(Camera.main.transform.position - transform.position, transform.forward):0.0} m" : "-";
        GUI.Box(new Rect(10, 10, 560, 190),
            $"Pista de distancias · vos: {vos} · muñecos {Nombre(mirada)}\n" +
            "Fin: hacia dónde miran los muñecos · Inicio: 100 de vida y 25 de escudo\n" + ultima, estilo);
    }

    // ---------- La pista ----------

    private void ArmarPista()
    {
        // Franja oscura con colisión: se puede caminar aunque se pase del piso de la escena.
        Caja("Pista", new Vector3(0f, -0.01f, largo * 0.5f), new Vector3(1.6f, 0.04f, largo + 1f), gris, true);
        for (int m = 0; m <= largo; m++)
        {
            bool cinco = m % 5 == 0;
            Caja($"{m} m", new Vector3(0f, 0.012f, m), new Vector3(cinco ? 2.4f : 1.6f, 0.01f, cinco ? 0.1f : 0.05f),
                cinco ? amarillo : blanco, false);
            TextMeshPro numero = Texto(transform, $"{m}", new Vector3(cinco ? 1.7f : 1.25f, 0.02f, m), cinco ? 1.4f : 0.9f,
                cinco ? new Color(1f, 0.82f, 0.1f) : Color.white);
            numero.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // acostado, se lee desde el 0
        }
    }

    private void Caja(string nombre, Vector3 lugar, Vector3 tamano, Material material, bool conColision)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        if (!conColision) Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(transform, false);
        c.transform.localPosition = lugar;
        c.transform.localScale = tamano;
        Renderer r = c.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static TextMeshPro Texto(Transform padre, string texto, Vector3 lugar, float tamano, Color color)
    {
        var go = new GameObject("Texto");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = tamano * 4f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(8f, 2f);
        return t;
    }

    private Material CrearMaterial(Color color)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var m = new Material(c.GetComponent<Renderer>().sharedMaterial) { color = color };
        Destroy(c);
        return m;
    }

    // En el editor, sin Play, se ve por dónde va a ir la pista.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * largo);
        for (int m = 0; m <= largo; m += 5)
            Gizmos.DrawWireCube(transform.position + transform.forward * m, new Vector3(2.4f, 0.02f, 0.1f));
        Gizmos.color = Color.cyan;
        if (distancias != null)
            foreach (int m in distancias)
                Gizmos.DrawWireCube(transform.position + transform.forward * m + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
        if (filas != null && zonasFila != null)
            foreach (int f in filas)
                for (int i = 0; i < zonasFila.Length; i++)
                    Gizmos.DrawWireCube(transform.position + transform.forward * f + transform.right * ((i - (zonasFila.Length - 1) * 0.5f) * separacionFila) + Vector3.up,
                        new Vector3(0.6f, 2f, 0.6f));
        if (tirador)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(transform.TransformPoint(lugarTirador) + Vector3.up, 0.6f);
            Gizmos.color = Color.cyan;
        }
        if (companeros != null)
            foreach (Vector3 o in companeros)
                Gizmos.DrawWireSphere(transform.TransformPoint(o) + Vector3.up, 0.5f);
        if (otros != null)
            foreach (Vector3 o in otros)
                Gizmos.DrawWireCube(transform.TransformPoint(o) + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
    }
}
