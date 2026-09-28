using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ScaleTestController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float mouseSensitivity = 2f;
    public float gravity = -15f;

    CharacterController controller;
    Camera cam;
    float pitch;
    Vector3 verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);
        pitch = Mathf.Clamp(pitch - mouseY, -89f, 89f);
        if (cam != null)
            cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        Vector3 move = (transform.right * h + transform.forward * v) * speed;

        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;
        verticalVelocity.y += gravity * Time.deltaTime;

        controller.Move((move + verticalVelocity) * Time.deltaTime);
    }
}
