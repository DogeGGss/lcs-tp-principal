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

    // Ajustes de agachado
    public float standingHeight = 2.0f;
    public float crounchHeight = 1.0f;

    private Vector3 standingCenter = new Vector3(0, 0, 0);
    private Vector3 crounchCenter = new Vector3(0, -0.5f, 0);

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

        bool isCrounching = KeyBindings.Held(GameAction.Agacharse);
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

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;


        // =====================================================
        // SALTO
        // =====================================================

        if (KeyBindings.Down(GameAction.Saltar) && controller.isGrounded)
        {
            verticalVelocity = jumpForce;

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

        Vector3 velocity = move * currentSpeed;
        velocity.y = verticalVelocity;

        CollisionFlags collisions =
            controller.Move(velocity * Time.deltaTime);


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
}