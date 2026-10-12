using UnityEngine;

public class CameraLook : MonoBehaviour
{
    public float mouseSensivility = 1.5f;

    public Transform playerBody;

    float xRotation = 0;

    private float sensibilidadApuntando = 1f;
    private bool invertirY;

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

        // Opciones de controles (US 155): sensibilidad al apuntar con mira y eje Y invertido.
        sensibilidadApuntando = KeyBindings.AimSensitivity;
        invertirY = KeyBindings.InvertY;

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
        // Con la mira telescópica puesta (US 009) el mouse va más lento en proporción al zoom, y encima se aplica la
        // sensibilidad de mira de Opciones (US 155).
        bool apuntando = MiraTelescopica.Puesta;
        float sensibilidad = apuntando
            ? mouseSensivility * sensibilidadApuntando * MiraTelescopica.EscalaSensibilidad
            : mouseSensivility;

        float mouseX =
            Input.GetAxis("Mouse X") * sensibilidad;

        float mouseY =
            Input.GetAxis("Mouse Y") * sensibilidad * (invertirY ? -1f : 1f);

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation =
            Quaternion.Euler(xRotation, 0, 0);

        playerBody.Rotate(Vector3.up * mouseX);
    }
}