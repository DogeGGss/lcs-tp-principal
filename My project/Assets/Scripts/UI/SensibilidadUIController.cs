using UnityEngine;
using UnityEngine.UI;

public class SensibilidadUIController : MonoBehaviour
{
    [SerializeField] private Slider sliderSensibilidad;

    private const string CLAVE_SENSIBILIDAD = "Sensibilidad";
    private const float SENSIBILIDAD_POR_DEFECTO = 1.5f;

    private void Start()
    {
        float sensibilidadGuardada = PlayerPrefs.GetFloat(
            CLAVE_SENSIBILIDAD,
            SENSIBILIDAD_POR_DEFECTO
        );

        sliderSensibilidad.minValue = 0.5f;
        sliderSensibilidad.maxValue = 3f;
        sliderSensibilidad.value = sensibilidadGuardada;

        sliderSensibilidad.onValueChanged.AddListener(CambiarSensibilidad);
    }

    private void CambiarSensibilidad(float valor)
    {
        PlayerPrefs.SetFloat(CLAVE_SENSIBILIDAD, valor);
        PlayerPrefs.Save();

        Debug.Log("Sensibilidad: " + valor);
    }

    public void RestablecerSensibilidad()
    {
        sliderSensibilidad.value = SENSIBILIDAD_POR_DEFECTO;

        PlayerPrefs.SetFloat(
            CLAVE_SENSIBILIDAD,
            SENSIBILIDAD_POR_DEFECTO
        );

        PlayerPrefs.Save();
    }
}