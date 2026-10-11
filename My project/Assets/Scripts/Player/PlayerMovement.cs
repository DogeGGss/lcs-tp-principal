using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;

    public float speed = 5f;
    public float sprintSpeed = 8f;
    public float crounchSpeed = 2.5f;
    [Tooltip("En el Táctico, Shift no corre: camina despacio a esta velocidad y sin ruido (US 198).")]
    public float slowWalkSpeed = 3f;
    public float gravity = -9.8f;
    public float jumpForce = 5f;

    public Animator animator;

    // Ajustes de agachado. Agachado, la cápsula (1,45 m) sigue tapando la cámara (a 1,3 m): si fuera más baja,
    // debajo de algo bajo la cámara quedaba adentro del objeto (US 004).
    public float standingHeight = 2.0f;
    public float crounchHeight = 1.45f;

    private Vector3 standingCenter = new Vector3(0, 0, 0);
    // Agachada, la cápsula sigue apoyada en el mismo piso: baja el centro la mitad de lo que se achica.
    private Vector3 crounchCenter => new Vector3(0, (crounchHeight - standingHeight) * 0.5f, 0);

    [Tooltip("US 008: lo más alto que sube caminando, sin saltar, en cualquier dirección. El \"Step Offset\" del " +
             "CharacterController está para que de frente llegue a esto; en diagonal lo frena SinTreparMasAlto.")]
    public float alturaMaximaEscalon = 0.5f;

    [HideInInspector] public float speedMultiplier = 1f;

    // Para el multijugador (US 025): los demás repiten el salto y el aterrizaje en su copia de este jugador.
    public event System.Action Jumped, Landed;

    // Para los pasos (Pasos): agachado o caminando despacio no suenan (US 198).
    public bool Agachado { get; private set; }
    public bool CaminandoDespacio { get; private set; }

    private float verticalVelocity;

    // Se utiliza para detectar correctamente el aterrizaje
    private bool wasInAir;

    //manejo de camara al agacharse
    public Transform cameraTransform;
    public float standingCameraHeight = 0.8f;
    public float crouchCameraHeight = 0.3f;
    public float cameraCrouchSpeed = 8f;


    private float moveX, moveZ;

    // Como el Input Manager (sensibilidad y gravedad 3, con snap): llega al valor en un tercio de segundo
    // y al cambiar de dirección pasa por cero de golpe.
    private static float SmoothAxis(float current, float target)
    {
        if (target != 0f && current != 0f && Mathf.Sign(target) != Mathf.Sign(current)) current = 0f;
        return Mathf.MoveTowards(current, target, 3f * Time.deltaTime);
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.height = standingHeight;
        controller.center = standingCenter;

        // Los pasos se agregan solos (US 001 y US 002): no hace falta tocar el prefab.
        if (GetComponent<Pasos>() == null) gameObject.AddComponent<Pasos>();
        // Igual lo que pasa al caerse del mapa (US 006).
        if (GetComponent<FueraDelMapa>() == null) gameObject.AddComponent<FueraDelMapa>();
    }

    /// <summary>US 006, CA6: al reaparecer no conserva la velocidad de la caída ni lo que venía moviéndose.</summary>
    public void Frenar()
    {
        verticalVelocity = 0f;
        moveX = moveZ = 0f;
        wasInAir = false;
        if (animator != null) animator.SetBool("isFalling", false);
    }


    private void Update()
    {
        // =====================================================
        // MOVIMIENTO
        // =====================================================

        // Teclas reasignables (US 155), suavizadas como Input.GetAxis.
        moveX = SmoothAxis(moveX, KeyBindings.Axis(GameAction.Izquierda, GameAction.Derecha));
        moveZ = SmoothAxis(moveZ, KeyBindings.Axis(GameAction.Atras, GameAction.Adelante));
        float x = moveX;
        float z = moveZ;

        animator.SetFloat("VelX", x);
        animator.SetFloat("VelZ", z);

        Vector3 move = transform.right * x
                     + transform.forward * z;

        move = Vector3.ClampMagnitude(move, 1f);


        // =====================================================
        // AGACHARSE
        // =====================================================

        // US 004: si suelta Ctrl con algo encima, sigue agachado hasta que haya lugar para pararse (si no, la cápsula
        // de 2 m quedaba metida en el objeto y no se podía mover).
        bool isCrounching = KeyBindings.Held(GameAction.Agacharse) || (Agachado && !HayLugarParaPararse());
        animator.SetBool("isCrouching", isCrounching);
        Agachado = isCrounching;

        if (isCrounching)
        {
            controller.height = crounchHeight;
            controller.center = crounchCenter;
        }
        else
        {
            controller.height = standingHeight;
            controller.center = standingCenter;
        }


        // =====================================================
        // CÁMARA
        // =====================================================

        float targetCameraHeight = isCrounching ? crouchCameraHeight : standingCameraHeight;
        Vector3 cameraPosition = cameraTransform.localPosition;

        cameraPosition.y = Mathf.Lerp(cameraPosition.y, targetCameraHeight, cameraCrouchSpeed * Time.deltaTime);
        cameraTransform.localPosition = cameraPosition;


        // =====================================================
        // VELOCIDAD / CORRER
        // =====================================================

        float currentSpeed = speed;

        bool isSprinting = false;
        bool despacio = false;

        if (isCrounching)
        {
            currentSpeed = crounchSpeed;
        }
        else if (KeyBindings.Held(GameAction.Correr))
        {
            // US 198: en el Táctico no se corre; Shift camina despacio y sin ruido.
            if (MatchSettings.Mode == GameMode.Tactico)
            {
                currentSpeed = slowWalkSpeed;
                despacio = true;
            }
            else
            {
                currentSpeed = sprintSpeed;
                isSprinting = true;
            }
        }
        CaminandoDespacio = despacio;

        // Caminando despacio, la animación de caminar va más lenta (los demás la ven igual: VelX y VelZ viajan por la red).
        if (despacio)
        {
            float lento = slowWalkSpeed / Mathf.Max(0.01f, speed);
            animator.SetFloat("VelX", x * lento);
            animator.SetFloat("VelZ", z * lento);
        }

        currentSpeed *= speedMultiplier;

        animator.SetBool("isSprinting", isSprinting);


        // =====================================================
        // GRAVEDAD
        // =====================================================

        // Apoyado al empezar el cuadro (lo dejó así el Move del cuadro anterior): para pegarlo a las rampas al final.
        bool estabaApoyado = controller.isGrounded;
        bool saltoEsteCuadro = false;

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }


        // =====================================================
        // SALTO
        // =====================================================

        if (KeyBindings.Down(GameAction.Saltar) && controller.isGrounded)
        {
            verticalVelocity = jumpForce;
            saltoEsteCuadro = true;

            // Inicia JumpUp
            animator.SetTrigger("Jump");

            // Ya no estamos cayendo
            animator.SetBool("isFalling", false);

            // Marcamos que estamos en el aire
            wasInAir = true;

            Jumped?.Invoke();
        }


        // =====================================================
        // DETECTAR QUE ESTÁ EN EL AIRE
        // =====================================================

        if (!controller.isGrounded)
        {
            wasInAir = true;
        }


        // =====================================================
        // DETECTAR CAÍDA
        // =====================================================

        if (!controller.isGrounded && verticalVelocity < 0)
        {
            animator.SetBool("isFalling", true);
        }


        // =====================================================
        // MOVIMIENTO DEL CHARACTER CONTROLLER
        // =====================================================

        // US 003, CA2: la gravedad se aplica con la cuenta exacta del movimiento con aceleración constante
        // (promedio de la velocidad al principio y al final del cuadro). Así el salto llega siempre a 1,28 m a los
        // 0,51 s, a cualquier cantidad de FPS (sumando velocidad * tiempo daba 1,32 m a 60 FPS y 1,36 m a 30).
        float dt = Time.deltaTime;
        float verticalInicio = verticalVelocity;
        verticalVelocity += gravity * dt;

        Vector3 velocity = move * currentSpeed;
        Vector3 desplazamiento = velocity * dt;
        desplazamiento.y = (verticalInicio + verticalVelocity) * 0.5f * dt;

        // US 008: apoyado y sin saltar, no encara bordes más altos que el escalón máximo (en diagonal los subía).
        if (estabaApoyado && !saltoEsteCuadro)
        {
            Vector3 plano = SinTreparMasAlto(controller, new Vector3(desplazamiento.x, 0f, desplazamiento.z), alturaMaximaEscalon);
            desplazamiento.x = plano.x;
            desplazamiento.z = plano.z;
        }

        CollisionFlags collisions =
            controller.Move(desplazamiento);

        // US 003, CA1: bajando una rampa el personaje se despegaba (baja más rápido de lo que lo empuja el -2 m/s)
        // y mientras tanto no podía saltar. Si venía apoyado y no saltó, se lo vuelve a apoyar en la rampa.
        if (estabaApoyado && !saltoEsteCuadro && verticalVelocity <= 0f && !controller.isGrounded)
            PegarAlPiso(new Vector2(desplazamiento.x, desplazamiento.z).magnitude);


        // =====================================================
        // DETECTAR ATERRIZAJE
        // =====================================================

        if (controller.isGrounded && wasInAir)
        {
            // Ya no estamos en el aire
            wasInAir = false;

            // Dejamos de estar en caída
            animator.SetBool("isFalling", false);

            // Activa JumpDown
            animator.SetTrigger("Land");

            Landed?.Invoke();
        }


        // =====================================================
        // CHOQUE CON EL TECHO
        // =====================================================

        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0)
        {
            verticalVelocity = 0f;
        }
    }

    // Lo que suma la cápsula al pararse (de lo alto de la cápsula agachada a lo alto de la parada) no choca con nada.
    // Un poco más angosta que el cuerpo, para no contar las paredes que ya está tocando.
    private bool HayLugarParaPararse()
    {
        float radio = controller.radius * 0.95f;
        Vector3 abajo = transform.TransformPoint(crounchCenter + Vector3.up * (crounchHeight * 0.5f - controller.radius));
        Vector3 arriba = transform.TransformPoint(standingCenter + Vector3.up * (standingHeight * 0.5f - controller.radius));
        foreach (Collider c in Physics.OverlapCapsule(abajo, arriba, radio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(transform)) return false;
        return true;
    }

    // US 008: el CharacterController sube bordes más altos que su "Step Offset" si los encara en diagonal (la base de
    // la cápsula es redonda y toma el borde como una rampa). Antes de moverse se mira, en una franja justo arriba de la
    // altura máxima, el frente de la cápsula: si ahí hay algo que no es una rampa caminable ni un techo, el movimiento
    // se desliza a lo largo de eso en vez de encararlo. Lo que es más bajo que la franja se sube como siempre.
    public static Vector3 SinTreparMasAlto(CharacterController cc, Vector3 horizontal, float alturaMaxima)
    {
        float largo = horizontal.magnitude;
        if (largo < 1e-5f) return horizontal;

        const float RadioSonda = 0.05f;
        Transform cuerpo = cc.transform;
        Vector3 centro = cuerpo.TransformPoint(cc.center);
        float piso = centro.y - cc.height * 0.5f - cc.skinWidth;
        float altura = piso + alturaMaxima + 0.03f + RadioSonda;
        if (altura + RadioSonda > centro.y + cc.height * 0.5f) return horizontal; // la cápsula no llega a la franja

        Vector3 hacia = horizontal / largo;
        float alcance = largo + cc.skinWidth + 0.02f;
        float limite = Mathf.Cos(cc.slopeLimit * Mathf.Deg2Rad);
        // Cinco sondas repartidas por el frente de la cápsula (de -80° a 80° de hacia dónde va), al borde del cuerpo.
        for (int i = -2; i <= 2; i++)
        {
            Vector3 lado = Quaternion.Euler(0f, i * 40f, 0f) * hacia;
            Vector3 origen = new Vector3(centro.x, altura, centro.z) + lado * (cc.radius - RadioSonda);
            foreach (RaycastHit golpe in Physics.SphereCastAll(origen, RadioSonda, hacia, alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (golpe.distance <= 0f || golpe.collider.transform.IsChildOf(cuerpo)) continue;
                if (golpe.normal.y >= limite || golpe.normal.y < -0.2f) continue; // rampa caminable, o techo
                Vector3 normal = new Vector3(golpe.normal.x, 0f, golpe.normal.z);
                if (normal.sqrMagnitude < 1e-6f) continue;
                normal.Normalize();
                float contra = Vector3.Dot(horizontal, normal);
                if (contra < 0f) horizontal -= normal * contra;
            }
        }
        return horizontal;
    }

    // Busca el piso justo debajo, a lo sumo a lo que bajaría una pendiente caminable (slopeLimit) en lo que se movió
    // este cuadro. Si hay piso caminable, lo baja hasta apoyarlo. Un borde de verdad (un escalón alto, el borde de
    // una losa) queda más lejos y el personaje cae normalmente. Devuelve si lo apoyó (lo usa también el Trasbordo, US 019).
    public bool PegarAlPiso(float avance)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        float maximo = avance * Mathf.Tan(controller.slopeLimit * Mathf.Deg2Rad) + controller.skinWidth + 0.05f;
        float radio = controller.radius * 0.9f;
        Vector3 centro = transform.TransformPoint(controller.center);
        Vector3 pie = centro + Vector3.down * (controller.height * 0.5f - controller.radius); // centro de la esfera de abajo

        float mejor = float.MaxValue;
        foreach (RaycastHit golpe in Physics.SphereCastAll(pie, radio, Vector3.down, maximo + (controller.radius - radio), ~0, QueryTriggerInteraction.Ignore))
        {
            if (golpe.collider.transform.IsChildOf(transform)) continue;       // el propio cuerpo, las armas
            if (golpe.distance <= 0f) continue;                                 // ya lo tocaba al empezar
            if (Vector3.Angle(golpe.normal, Vector3.up) > controller.slopeLimit) continue; // pared, no piso
            if (golpe.distance < mejor) mejor = golpe.distance;
        }
        if (mejor == float.MaxValue) return false;

        controller.Move(Vector3.down * (mejor + controller.skinWidth));
        return true;
    }
}