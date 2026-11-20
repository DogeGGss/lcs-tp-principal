using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El ataque cuerpo a cuerpo: el cuchillo y, en el Modo Invasión, la katana (US 395, KatanaData), que ocupa su lugar y
// suma lo propio:
// - CA3: cada tajo pega a todos los que tiene delante (no solo al más cercano a la mira).
// - CA4: el tercer golpe seguido (cada uno a menos de 1,2 s del anterior) es un tajo fuerte: el doble de daño y un
//   empujón.
// - CA5: manteniendo el ataque 0,6 s, una estocada: el jugador avanza 5 m (sin atravesar paredes) y le pega a cada
//   invasor que cruza. Después tarda 6 s en poder volver a hacerla.
// - CA6: con el clic derecho, la guardia: frena los disparos de los invasores que llegan de frente (FrenaDisparo), no
//   se puede atacar y se camina más lento.
// La cura por baja (CA7) y lo que se ve (CA9) los pone KatanaDeInvasion.
public class MeleeAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MeleeWeaponHolder weaponHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioClip swingSound;
    private AudioSource audioSource;

    private float nextTimeToAttack = 0f;
    private bool isAttacking = false;

    // Cada ataque con el cuchillo: el multijugador lo repite en las demás computadoras (US 028).
    public static event System.Action<MeleeAttack> Swung;
    // Cada baja con el cuchillo (la carta Filo de Invasión recarga el arma: US 317).
    public static event System.Action<MeleeAttack> Mato;
    // Cambia el daño de un cuchillazo antes de aplicarlo (la carta Remate de Invasión: US 328). Recibe el cuchillo, a
    // quién le pega y el daño del cuchillo; devuelve el daño que le hace.
    public static System.Func<MeleeAttack, HealthSystem, int, int> AjustarGolpe;
    // Cada cuchillazo que le sacó vida o escudo a alguien: el cuchillo, a quién y cuánto (la carta Filo voraz: US 327).
    public static event System.Action<MeleeAttack, HealthSystem, int> Golpeo;
    public AudioClip SwingSound => swingSound;
    // US 395, CA6: la katana frenó un disparo (el ataque y dónde se vio el choque).
    public static event System.Action<MeleeAttack, Vector3> Freno;
    // US 395: cada golpe de la katana que tocó a alguien (para el sonido y el brillo).
    public static event System.Action<MeleeAttack> PegoLaKatana;

    // US 256: el corte de costado. Lo dibuja AnimacionPrimeraPersona con el avance (AvanceDelCorte); acá solo se mide el
    // tiempo: el golpe llega en la mitad del corte y todo termina antes de que se pueda volver a atacar (0,75 s).
    public const float Preparacion = 0.12f; // la mano lleva el cuchillo hacia atrás y hacia adentro
    public const float Corte = 0.12f;       // el corte cruza de adentro hacia afuera; el golpe, en la mitad
    public const float Vuelta = 0.30f;      // vuelve a su lugar
    public const float Total = Preparacion + Corte + Vuelta;
    private float inicioDelCorte;

    /// <summary>De 0 a 1 mientras corta, o -1.</summary>
    public float AvanceDelCorte => isAttacking ? Mathf.Clamp01((Time.time - inicioDelCorte) / Total) : -1f;

    // ---------- US 395: la katana ----------
    private PlayerMovement movimiento;
    private CharacterController cuerpo;
    private int golpesSeguidos;
    private float ultimoGolpe = -10f;
    private bool fuerte;
    private bool estocando;
    private float estocadaDesde, estocadaDura = 0.25f, estocadaListaEn;
    private float apretadoDesde = -1f;
    private bool estocadaEnEsteApreton;
    private bool enGuardia;
    private static MeleeAttack enGuardiaAhora;

    /// <summary>La katana, si es lo que hay en el lugar del cuchillo (null con el cuchillo).</summary>
    public KatanaData Katana => weaponHolder != null ? weaponHolder.CurrentWeapon as KatanaData : null;
    /// <summary>CA4: el corte que está haciendo es el tajo fuerte.</summary>
    public bool TajoFuerte => isAttacking && fuerte;
    /// <summary>CA5: de 0 a 1 mientras hace la estocada, o -1.</summary>
    public float AvanceDeLaEstocada => estocando ? Mathf.Clamp01((Time.time - estocadaDesde) / estocadaDura) : -1f;
    /// <summary>CA6: de 0 (sin guardia) a 1 (en guardia), suavizado para el movimiento.</summary>
    public float Guardia { get; private set; }
    /// <summary>CA6: está en guardia.</summary>
    public bool EnGuardia => enGuardia;

    private bool EnMano => weaponHolder != null && weaponHolder.CurrentViewModel != null && weaponHolder.CurrentViewModel.activeInHierarchy;

    private void Start()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<MeleeWeaponHolder>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        movimiento = GetComponent<PlayerMovement>();
        cuerpo = GetComponent<CharacterController>();
    }

    private void OnDisable() => PonerGuardia(false, null);

    private void Update()
    {
        KatanaData katana = Katana;
        bool enMano = EnMano;

        // US 395, CA6: la guardia, mientras mantiene el clic derecho con la katana en la mano.
        bool guardia = katana != null && enMano && !estocando && !isAttacking && KeyBindings.Held(GameAction.Apuntar);
        PonerGuardia(guardia, katana);
        Guardia = Mathf.MoveTowards(Guardia, enGuardia ? 1f : 0f, Time.deltaTime / 0.12f);

        // CA1: Clic izquierdo para atacar
        if (KeyBindings.Down(GameAction.Disparar))
        {
            apretadoDesde = Time.time;
            estocadaEnEsteApreton = false;
            TryAttack();
        }

        // US 395, CA5: manteniendo el ataque, la estocada (una por apretón).
        if (apretadoDesde >= 0f && KeyBindings.Held(GameAction.Disparar))
        {
            if (katana != null && enMano && !estocadaEnEsteApreton && !estocando && !enGuardia &&
                Time.time - apretadoDesde >= katana.cargaDeLaEstocada && Time.time >= estocadaListaEn)
            {
                estocadaEnEsteApreton = true;
                StartCoroutine(Estocada(katana));
            }
        }
        else apretadoDesde = -1f;
    }

    // CA6: entra o sale de la guardia: frena al jugador y avisa quién la tiene puesta (para FrenaDisparo).
    private void PonerGuardia(bool poner, KatanaData katana)
    {
        if (poner == enGuardia) return;
        enGuardia = poner;
        if (movimiento != null) movimiento.frenoExtra = poner && katana != null ? katana.velocidadEnGuardia : 1f;
        if (poner) enGuardiaAhora = this;
        else if (enGuardiaAhora == this) enGuardiaAhora = null;
    }

    public void TryAttack()
    {
        // Verifica si hay un arma equipada
        if (weaponHolder == null || weaponHolder.CurrentWeapon == null) return;

        // Si el cuchillo está oculto porque tenés la pistola en mano, no ataca
        if (weaponHolder.CurrentViewModel == null || !weaponHolder.CurrentViewModel.activeInHierarchy) return;

        // Verifica que no esté atacando ni en cooldown (ni haciendo la estocada o en guardia, con la katana)
        if (isAttacking || estocando || enGuardia || Time.time < nextTimeToAttack) return;

        MeleeWeaponData data = weaponHolder.CurrentWeapon;
        nextTimeToAttack = Time.time + data.cooldown;

        // US 395, CA4: los golpes seguidos de la katana; el último del combo es el tajo fuerte.
        fuerte = false;
        if (data is KatanaData katana)
        {
            golpesSeguidos = Time.time - ultimoGolpe <= katana.ventanaDelCombo ? golpesSeguidos + 1 : 1;
            ultimoGolpe = Time.time;
            fuerte = KatanaData.EsTajoFuerte(golpesSeguidos, katana.golpesDelCombo);
        }

        StartCoroutine(AttackRoutine(data));
    }

    private IEnumerator AttackRoutine(MeleeWeaponData data)
    {
        isAttacking = true;
        Swung?.Invoke(this);

        // CA6: Sonido de ataque (la katana tiene el suyo, más fuerte en el tajo fuerte: US 395)
        AudioClip sonido = data is KatanaData ? SonidosDeKatana.Corte(fuerte) : swingSound;
        if (sonido != null)
            audioSource.PlayOneShot(sonido);

        GameObject viewModel = weaponHolder.CurrentViewModel;
        inicioDelCorte = Time.time;
        if (!(data is KatanaData)) CalorDeSkin.Disparo(viewModel); // F35: el fuego de la skin del cuchillo se aviva al cortar

        // CA3 (US 256): el golpe, en la mitad del corte. CA4: si cambió de arma antes, el corte se interrumpe sin golpe.
        yield return new WaitForSeconds(Preparacion + Corte * 0.5f);
        if (viewModel == null || !viewModel.activeInHierarchy) { isAttacking = false; yield break; }
        ExecuteHitCheck(data);

        // El resto del corte y la vuelta (también se corta si guarda el cuchillo).
        while (Time.time - inicioDelCorte < Total)
        {
            if (viewModel == null || !viewModel.activeInHierarchy) break;
            yield return null;
        }
        isAttacking = false;
    }

    private void ExecuteHitCheck(MeleeWeaponData data)
    {
        if (MinijuegoCometas.Activo) return; // US 157, CA7: en la compra el cuchillo se usa, pero no hace daño
        KatanaData katana = data as KatanaData;
        // CA2: Busca todos los colliders dentro del rango del arma, con las zonas de impacto de los huesos (US 165)
        Vector3 origin = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        Collider[] hitColliders = Physics.OverlapSphere(origin, data.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        HealthSystem best = null;
        float bestAngle = float.MaxValue;
        var todos = katana != null ? new List<HealthSystem>() : null; // US 395, CA3: la katana le pega a todos
        foreach (Collider col in hitColliders)
        {
            // Ignorar al propio jugador y los triggers que no son zonas de impacto (como la zona de compra)
            if (col.transform.root == transform.root) continue;
            if (col.isTrigger && col.GetComponent<HitZone>() == null) continue;

            HealthSystem health = col.GetComponentInParent<HealthSystem>();
            if (health == null || health.currentHealth <= 0 || EquiposTacticos.SonAliados(this, health)) continue; // US 031, CA6
            if (todos != null && todos.Contains(health)) continue;

            // CA2: Comprobación del cono angular (attackAngle), hasta el punto más cercano del collider y no hasta su
            // centro: así un rival parado entra en el cono aunque su centro quede más abajo que los ojos.
            Vector3 toTarget = ClosestPoint(col, origin) - origin;
            float distance = toTarget.magnitude;
            float angleToTarget = distance < 0.001f ? 0f : Vector3.Angle(forward, toTarget);
            if (angleToTarget > data.attackAngle / 2f || (todos == null && angleToTarget >= bestAngle)) continue;

            // Verificar que no haya una pared en el medio (sin contar triggers, como las zonas de impacto de US 165)
            if (distance >= 0.001f && Physics.Raycast(origin, toTarget / distance, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<HealthSystem>() != health && hit.collider.transform.root != transform.root) continue;

            if (todos != null) { todos.Add(health); continue; }
            best = health;
            bestAngle = angleToTarget;
        }

        // US 395, CA3 y CA4: la katana, a todos los del abanico (el tajo fuerte, con más daño y un empujón).
        if (todos != null && todos.Count > 0)
        {
            int danioKatana = Mathf.RoundToInt(data.damage * (fuerte ? katana.danioDelTajoFuerte : 1f));
            foreach (HealthSystem h in todos)
            {
                Golpear(h, danioKatana, origin);
                if (fuerte && katana.empujonDelTajoFuerte > 0f)
                {
                    Npc npc = h.GetComponentInParent<Npc>();
                    if (npc != null && npc.Vivo) EmpujonDeNpc.Empujar(npc, npc.transform.position - transform.position, katana.empujonDelTajoFuerte);
                }
            }
            PegoLaKatana?.Invoke(this);
        }
        // CA3: Aplicar daño al HealthSystem. Golpea a un solo objetivo por ataque: el más cercano a la mira.
        else if (best != null)
        {
            Golpear(best, data.damage, origin);
        }
        // US 020, CA5: sin nadie a tiro, el cuchillo le pega a la Barrera que tenga adelante (si es de un rival).
        else if (Physics.Raycast(origin, forward, out RaycastHit pared, data.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Barrera barrera = Barrera.De(pared.collider);
            if (barrera != null)
            {
                barrera.Chispas(pared.point, pared.normal);
                barrera.PedirDanio(data.damage, this, pared.point);
            }
            // US 022, CA9: también al perro del Callejero de un rival.
            Perro perro = Perro.De(pared.collider);
            if (perro != null && perro.PedirDanio(data.damage, this, pared.point)) WeaponFire.ReportMelee(false);
        }
    }

    // Un golpe a alguien: con las cartas que cambian el golpe (Remate), el marcador, el aviso de baja y las cartas que
    // reaccionan al daño o a la baja (Filo, Filo voraz; y la cura de la katana, US 395).
    private void Golpear(HealthSystem objetivo, int danioBase, Vector3 origin)
    {
        // Se mira antes de aplicar el daño (a un jugador de otra computadora le llega después), para el marcador
        // de impacto y el aviso de baja: antes las bajas con cuchillo no salían en los avisos sin conexión.
        int danio = AjustarGolpe != null ? AjustarGolpe(this, objetivo, danioBase) : danioBase;
        bool lethal = objetivo.WouldDie(danio);
        int antes = objetivo.currentHealth + objetivo.currentShield;
        HealthSystem.DamageOrigin = origin; // US 192
        objetivo.TakeDamage(danio);
        int hecho = Mathf.Max(0, antes - (objetivo.currentHealth + objetivo.currentShield));
        WeaponFire.ReportMelee(lethal);
        if (hecho > 0) Golpeo?.Invoke(this, objetivo, hecho);
        if (lethal) Mato?.Invoke(this);
    }

    // ---------- US 395, CA5: la estocada ----------

    private IEnumerator Estocada(KatanaData katana)
    {
        estocando = true;
        estocadaDesde = Time.time;
        estocadaDura = Mathf.Max(0.05f, katana.duracionDeLaEstocada);
        estocadaListaEn = Time.time + katana.esperaDeLaEstocada;
        Swung?.Invoke(this); // los demás ven un corte
        AudioClip sonido = SonidosDeKatana.Estocada();
        if (sonido != null) audioSource.PlayOneShot(sonido);

        Vector3 dir = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();
        var tocados = new HashSet<HealthSystem>();
        float recorrido = 0f, velocidad = katana.largoDeLaEstocada / estocadaDura;
        bool pego = false;
        while (recorrido < katana.largoDeLaEstocada && EnMano && cuerpo != null && cuerpo.enabled)
        {
            float paso = Mathf.Min(katana.largoDeLaEstocada - recorrido, velocidad * Time.deltaTime);
            Vector3 antes = transform.position;
            cuerpo.Move(dir * paso); // el CharacterController no atraviesa paredes
            Vector3 despues = transform.position;
            recorrido += paso;
            pego |= GolpearEnElCamino(antes, despues, katana, tocados);
            if (paso > 0.001f && Vector3.Dot(despues - antes, dir) < paso * 0.3f) break; // chocó con algo
            yield return null;
        }
        if (pego) PegoLaKatana?.Invoke(this);
        // (que no corte enseguida al soltar)
        nextTimeToAttack = Mathf.Max(nextTimeToAttack, Time.time + 0.15f);
        estocando = false;
    }

    // Le pega una vez a cada uno que esté cerca del tramo recorrido (los que no tocó antes en esta estocada).
    private bool GolpearEnElCamino(Vector3 desde, Vector3 hasta, KatanaData katana, HashSet<HealthSystem> tocados)
    {
        if (MinijuegoCometas.Activo) return false;
        bool pego = false;
        foreach (Collider col in Physics.OverlapCapsule(desde, hasta, katana.anchoDeLaEstocada, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (col.transform.root == transform.root) continue;
            if (col.isTrigger && col.GetComponent<HitZone>() == null) continue;
            HealthSystem h = col.GetComponentInParent<HealthSystem>();
            if (h == null || h.currentHealth <= 0 || tocados.Contains(h) || EquiposTacticos.SonAliados(this, h)) continue;
            tocados.Add(h);
            Golpear(h, katana.danioDeLaEstocada, hasta);
            pego = true;
        }
        return pego;
    }

    // ---------- US 395, CA6: la guardia ----------

    /// <summary>CA6: si el jugador de esa vida está en guardia con la katana y el disparo que viene desde ese lugar le
    /// llega de frente, lo frena (no le hace daño). Lo llaman los disparos de los invasores antes de hacer daño.</summary>
    public static bool FrenaDisparo(HealthSystem vida, Vector3 desde)
    {
        MeleeAttack g = enGuardiaAhora;
        if (g == null || !g.enGuardia || vida == null || g.playerCamera == null) return false;
        KatanaData katana = g.Katana;
        if (katana == null || vida.transform.root != g.transform.root) return false;
        Transform ojos = g.playerCamera.transform;
        Vector3 hacia = desde - ojos.position, adelante = ojos.forward;
        hacia.y = 0f;
        adelante.y = 0f;
        if (hacia.sqrMagnitude > 0.0001f && adelante.sqrMagnitude > 0.0001f && Vector3.Angle(adelante, hacia) > katana.anguloDeLaGuardia * 0.5f)
            return false;
        Vector3 choque = ojos.position + ojos.forward * 0.7f + (desde - ojos.position).normalized * 0.15f;
        Freno?.Invoke(g, choque);
        return true;
    }

    // ClosestPoint solo sirve con cajas, esferas, cápsulas y mallas convexas; para el resto alcanza con su caja.
    private static Vector3 ClosestPoint(Collider col, Vector3 point)
    {
        if (col is MeshCollider mesh && !mesh.convex) return col.bounds.ClosestPoint(point);
        return col.ClosestPoint(point);
    }
}