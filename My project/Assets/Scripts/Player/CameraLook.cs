using UnityEngine;

public class CameraLook : MonoBehaviour
{

    public float mouseSensivility = 1.5f;

    public Transform playerBody;

    float xRotation = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        // El delta del mouse ya es por frame: multiplicarlo por Time.deltaTime haria que la sensibilidad dependa de los FPS.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensivility;

        float mouseY = Input.GetAxis("Mouse Y") * mouseSensivility;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        playerBody.Rotate(Vector3.up * mouseX);

    }
}
