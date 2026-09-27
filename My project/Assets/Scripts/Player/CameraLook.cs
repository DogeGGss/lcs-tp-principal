using UnityEngine;

public class CameraLook : MonoBehaviour
{
    public float mouseSensivility = 1.5f;

    public Transform playerBody;

    float xRotation = 0;

    private const string CLAVE_SENSIBILIDAD = "Sensibilidad";
    private const float SENSIBILIDAD_POR_DEFECTO = 1.5f;

    private const string CLAVE_FOV = "FOV";
    private const float FOV_POR_DEFECTO = 80f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        CargarAjustes();
    }

    // Se vuelve a leer cada vez que el componente se activa: al volver del menú de pausa (US 051)
    // o al cerrar la tienda, se aplica la sensibilidad y el FOV que se hayan cambiado.
    void OnEnable()
    {
        CargarAjustes();
    }

    private void CargarAjustes()
    {
        // =========================
        // SENSIBILIDAD
        // =========================

        mouseSensivility = PlayerPrefs.GetFloat(
            CLAVE_SENSIBILIDAD,
            SENSIBILIDAD_POR_DEFECTO
        );

        // =========================
        // CAMPO DE VISIÓN
        // =========================

        float fovGuardado = PlayerPrefs.GetFloat(
            CLAVE_FOV,
            FOV_POR_DEFECTO
        );

        Camera camara = GetComponent<Camera>();

        if (camara != null)
        {
            camara.fieldOfView = fovGuardado;
        }
    }

    void Update()
    {
        float mouseX =
            Input.GetAxis("Mouse X") * mouseSensivility;

        float mouseY =
            Input.GetAxis("Mouse Y") * mouseSensivility;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation =
            Quaternion.Euler(xRotation, 0, 0);

        playerBody.Rotate(Vector3.up * mouseX);
    }
}