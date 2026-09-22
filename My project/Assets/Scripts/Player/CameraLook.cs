using UnityEngine;

public class CameraLook : MonoBehaviour
{

    public float mouseSensivility = 80f;

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
        float mouseX = Input.GetAxis("Mouse X")*mouseSensivility * Time.deltaTime;

        float mouseY = Input.GetAxis("Mouse Y")*mouseSensivility * Time.deltaTime;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        playerBody.Rotate(Vector3.up * mouseX);

    }
}
