using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;

    public float speed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -9.8f;
    public float jumpForce = 5f;

    public Animator animator;

    [HideInInspector] public float speedMultiplier = 1f;

    private float verticalVelocity;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        //animaciones
        animator.SetFloat("VelX", x);
        animator.SetFloat("VelZ", z);

        Vector3 move = transform.right * x
                     + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        float currentSpeed = (Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed) * speedMultiplier;
        Vector3 velocity = move * currentSpeed;
        velocity.y = verticalVelocity;

        CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);

        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0)
        {
            verticalVelocity = 0f;
        }

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            verticalVelocity = jumpForce;
        }
    }
}
