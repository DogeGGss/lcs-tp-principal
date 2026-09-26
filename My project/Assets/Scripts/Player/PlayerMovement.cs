using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;

    public float speed = 5f;
    public float sprintSpeed = 8f;
    public float crounchSpeed = 2.5f;
    public float gravity = -9.8f;
    public float jumpForce = 5f;

    public Animator animator;

    // Ajustes de agachado
    public float standingHeight = 2.0f;
    public float crounchHeight = 1.0f;

    private Vector3 standingCenter = new Vector3(0, 0, 0);
    private Vector3 crounchCenter = new Vector3(0, -0.5f, 0);

    [HideInInspector] public float speedMultiplier = 1f;

    private float verticalVelocity;

    // Se utiliza para detectar correctamente el aterrizaje
    private bool wasInAir;


    private void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.height = standingHeight;
        controller.center = standingCenter;
    }


    private void Update()
    {
        // =====================================================
        // MOVIMIENTO
        // =====================================================

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        animator.SetFloat("VelX", x);
        animator.SetFloat("VelZ", z);

        Vector3 move = transform.right * x
                     + transform.forward * z;

        move = Vector3.ClampMagnitude(move, 1f);


        // =====================================================
        // AGACHARSE
        // =====================================================

        bool isCrounching = Input.GetKey(KeyCode.LeftControl);

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
        // VELOCIDAD / CORRER
        // =====================================================

        float currentSpeed = speed;

        bool isSprinting = false;

        if (isCrounching)
        {
            currentSpeed = crounchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = sprintSpeed;
            isSprinting = true;
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

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            verticalVelocity = jumpForce;

            // Inicia JumpUp
            animator.SetTrigger("Jump");

            // Ya no estamos cayendo
            animator.SetBool("isFalling", false);

            // Marcamos que estamos en el aire
            wasInAir = true;
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