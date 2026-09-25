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

    //Ajustes de agachado
    public float standingHeight = 2.0f;
    public float crounchHeight = 1.0f;
    private Vector3 standingCenter = new Vector3(0, 0, 0);
    private Vector3 crounchCenter = new Vector3(0, -0.5f, 0);

    [HideInInspector] public float speedMultiplier = 1f;

    private float verticalVelocity;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        controller.center = standingCenter;
    }

    private void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x
                     + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        //Lógica agacharse (Ctrl izq)
        bool isCrounching = Input.GetKey(KeyCode.LeftControl);

        if(isCrounching)
        {
            controller.height = crounchHeight;
            controller.center = crounchCenter;
        }
        else
        {
            controller.height = standingHeight;
            controller.center = standingCenter;
        }

        //verifica velocidad actual
        float currentSpeed = speed;
        if(isCrounching)
        {
            currentSpeed = crounchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = sprintSpeed;
        }
        //el arma equipada modifica la velocidad (cuchillo x1,15, US 062)
        currentSpeed *= speedMultiplier;

        verticalVelocity += gravity * Time.deltaTime;


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
