using UnityEngine;

// US 020 · Barrera: se mantiene la tecla para ver dónde va a quedar (verde si se puede, roja si no) y al soltarla se
// levanta una pared de 4 × 2,2 m a hasta 6 m, mirando hacia el jugador (CA1). El clic derecho cancela sin gastar nada.
// Solo va sobre piso caminable, no en el aire, y no se pone encima de jugadores, paredes ni muros de la base (CA2).
// La pared en sí (vida, grietas, sonidos, red) es Barrera; acá está solo el apuntado.
public class HabilidadBarrera : EfectoHabilidad
{
    public const float Alcance = 6f;              // CA1
    private const float PisoMinimo = 0.7f;        // normal.y de un piso caminable (hasta unos 45°)
    private const float Desnivel = 0.9f;          // CA2: entre las puntas no puede haber más que esto
    private static readonly Color Valido = new Color(0.3f, 1f, 0.45f, 0.35f);
    private static readonly Color Invalido = new Color(1f, 0.3f, 0.25f, 0.35f);

    /// <summary>CA11: el jugador que la levantó, su número de barrera, dónde (la base, al centro) y hacia dónde mira.
    /// Lo manda a los demás JugadorEnRed.</summary>
    public static event System.Action<Transform, int, Vector3, float> Levantada;

    private static int siguienteId = 1;
    private static Material materialFantasma;

    private PlayerAbility habilidad;
    private Transform ojos;
    private GameObject fantasma;
    private Renderer fantasmaMalla;
    private Barrera actual;
    private bool apuntando;
    private readonly RaycastHit[] rayos = new RaycastHit[16];
    private readonly Collider[] encima = new Collider[16];

    public override bool ApuntaAntes => true;
    public override bool RecargaAlTerminar => true;

    // CA10: mientras la pared está en pie, el reloj de la habilidad muestra su vida.
    public override float ProgresoActivo => actual != null && actual.Activa ? actual.Vida / (float)Barrera.VidaMax : -1f;
    public override string TextoActivo => actual != null && actual.Activa ? actual.Vida.ToString() : null;

    // Se usa al soltar la tecla (Update), no al apretarla.
    public override bool Aplicar() => false;

    private void Awake()
    {
        habilidad = GetComponent<PlayerAbility>();
    }

    private void OnDisable() => Cancelar();

    private void OnDestroy()
    {
        if (fantasma != null) Destroy(fantasma);
    }

    private void Update()
    {
        if (habilidad == null || !habilidad.enabled) return;
        if (actual != null && !actual.Activa) actual = null;

        bool bloqueado = ShopUI.IsOpen || PauseMenu.IsPaused || !PuedeUsar();
        if (!apuntando)
        {
            // CA8: una sola a la vez (con la pared en pie tampoco hay carga).
            if (!bloqueado && actual == null && habilidad.IsReady && KeyBindings.Down(GameAction.Habilidad)) apuntando = true;
            else return;
        }

        if (bloqueado || !habilidad.IsReady || KeyBindings.Down(GameAction.Apuntar)) { Cancelar(); return; }

        bool valido = Lugar(out Vector3 lugar, out float yaw);
        MostrarFantasma(lugar, yaw, valido);

        // Al soltar la tecla (o si se soltó mientras no se leía, por ejemplo con la pausa abierta).
        if (KeyBindings.Up(GameAction.Habilidad) || !KeyBindings.Held(GameAction.Habilidad))
        {
            Cancelar();
            if (valido) Levantar(lugar, yaw);
        }
    }

    private void Cancelar()
    {
        apuntando = false;
        if (fantasma != null) fantasma.SetActive(false);
    }

    private void Levantar(Vector3 lugar, float yaw)
    {
        if (!habilidad.Usar()) return;
        JugadorEnRed red = GetComponent<JugadorEnRed>();
        int id = siguienteId++;
        actual = Barrera.Crear(transform, red != null ? red.Actor : -1, id, lugar, yaw, habilidad, null);
        Levantada?.Invoke(transform, id, lugar, yaw);
    }

    // ---------- Dónde va ----------

    // CA1 y CA2. Devuelve dónde iría la pared (la base, al centro) y si se puede poner ahí.
    private bool Lugar(out Vector3 lugar, out float yaw)
    {
        Transform vista = Ojos();
        Vector3 origen = vista.position, direccion = vista.forward;
        Vector3 plano = new Vector3(direccion.x, 0f, direccion.z);
        if (plano.sqrMagnitude < 1e-4f) plano = transform.forward;
        yaw = Quaternion.LookRotation(plano.normalized, Vector3.up).eulerAngles.y;

        // Donde mira, hasta 6 m. Si mira a una pared o al aire, se busca el piso justo debajo.
        bool piso = false;
        if (Rayo(origen, direccion, Alcance, out RaycastHit golpe))
        {
            lugar = golpe.point;
            if (EsPiso(golpe)) piso = true;
            else lugar -= direccion * 0.3f; // un poco antes de la pared
        }
        else lugar = origen + direccion * Alcance;

        if (!piso)
        {
            if (Rayo(lugar + Vector3.up * 0.1f, Vector3.down, 3f, out RaycastHit abajo) && EsPiso(abajo)) { lugar = abajo.point; piso = true; }
            else return false; // CA2: no en el aire
        }

        // Tiene que verse desde los ojos (no se pone del otro lado de una pared).
        Vector3 arriba = lugar + Vector3.up * 0.3f;
        Vector3 hasta = arriba - origen;
        if (Rayo(origen, hasta.normalized, hasta.magnitude, out RaycastHit _)) return false;

        // CA2: piso debajo de las dos puntas y del centro, sin escalones grandes en el medio.
        Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
        Vector3 derecha = giro * Vector3.right;
        float bajo = float.MaxValue, alto = float.MinValue;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 punta = lugar + derecha * (i * (Barrera.Ancho * 0.5f - 0.2f));
            if (!Rayo(punta + Vector3.up * 1f, Vector3.down, 2.5f, out RaycastHit suelo) || !EsPiso(suelo)) return false;
            bajo = Mathf.Min(bajo, suelo.point.y);
            alto = Mathf.Max(alto, suelo.point.y);
        }
        if (alto - bajo > Desnivel) return false;
        lugar.y = bajo - 0.1f; // un poco enterrada: así no queda una rendija abajo en las rampas

        // CA2: nada en el lugar que va a ocupar (jugadores, también el propio, paredes, muros de la base, otra barrera).
        float desde = alto + 0.15f, techo = lugar.y + Barrera.Alto;
        Vector3 centro = new Vector3(lugar.x, (desde + techo) * 0.5f, lugar.z);
        Vector3 medio = new Vector3(Barrera.Ancho * 0.5f, (techo - desde) * 0.5f, Barrera.Grosor * 0.5f + 0.05f);
        int n = Physics.OverlapBoxNonAlloc(centro, medio, encima, giro, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (encima[i].GetComponentInParent<ArmaEnPiso>() != null) continue; // un arma tirada no molesta
            return false;
        }
        return true;
    }

    private bool EsPiso(RaycastHit golpe) =>
        golpe.normal.y >= PisoMinimo && golpe.collider.GetComponentInParent<HealthSystem>() == null &&
        golpe.collider.GetComponentInParent<Barrera>() == null;

    // El primer collider sólido que toca, sin contar los del propio jugador.
    private bool Rayo(Vector3 origen, Vector3 direccion, float distancia, out RaycastHit mejor)
    {
        mejor = default;
        int n = Physics.RaycastNonAlloc(origen, direccion, rayos, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float cerca = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (rayos[i].collider.transform.IsChildOf(transform)) continue;
            if (rayos[i].distance < cerca) { cerca = rayos[i].distance; mejor = rayos[i]; }
        }
        return cerca < float.MaxValue;
    }

    private Transform Ojos()
    {
        if (ojos == null)
        {
            foreach (Camera c in GetComponentsInChildren<Camera>())
                if (c.CompareTag("MainCamera")) { ojos = c.transform; break; }
            if (ojos == null && Camera.main != null) ojos = Camera.main.transform;
        }
        return ojos != null ? ojos : transform;
    }

    // ---------- Vista previa (CA1) ----------

    private void MostrarFantasma(Vector3 lugar, float yaw, bool valido)
    {
        if (fantasma == null)
        {
            fantasma = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fantasma.name = "Barrera (vista previa)";
            fantasma.layer = 2; // Ignore Raycast
            Destroy(fantasma.GetComponent<Collider>());
            fantasmaMalla = fantasma.GetComponent<Renderer>();
            fantasmaMalla.sharedMaterial = MaterialFantasma();
            fantasmaMalla.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fantasmaMalla.receiveShadows = false;
        }
        fantasma.SetActive(true);
        fantasma.transform.SetPositionAndRotation(lugar + Vector3.up * (Barrera.Alto * 0.5f), Quaternion.Euler(0f, yaw, 0f));
        fantasma.transform.localScale = new Vector3(Barrera.Ancho, Barrera.Alto, Barrera.Grosor);
        if (fantasmaMalla.sharedMaterial != null) fantasmaMalla.material.color = valido ? Valido : Invalido;
    }

    private static Material MaterialFantasma()
    {
        if (materialFantasma == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) materialFantasma = new Material(shader) { name = "Barrera (vista previa)" };
        }
        return materialFantasma;
    }
}
