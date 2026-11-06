using UnityEngine;

// Apuntar con el click derecho en las armas que no tienen mira telescópica, cuando una carta de Invasión lo da (US 319,
// Bala compacta, en la escopeta; US 323, Disparo concentrado, en el Mitre): mientras se mantiene apretado, un zoom
// chico (el campo de visión se achica un 20 %); al soltar, o al recargar o guardar el arma, vuelve. Cada arma tiene el
// suyo y lo actualiza en cada cuadro.
public class ZoomPorCarta
{
    private const float Zoom = 1.2f, Tiempo = 0.12f;

    /// <summary>Alguna arma está apuntando así ahora (para la mira dinámica del HUD).</summary>
    public static bool AlgunaApuntando => activo != null && activo.Apuntando;
    private static ZoomPorCarta activo;

    public bool Apuntando { get; private set; }
    private bool fovCambiado;
    private float fovNormal;
    private Camera camara;

    /// <summary>puede: el arma está lista y la carta lo permite. Devuelve si está apuntando.</summary>
    public bool Actualizar(bool puede, Camera cam)
    {
        Apuntando = puede && KeyBindings.Held(GameAction.Apuntar) && !ShopUI.IsOpen;
        if (Apuntando) activo = this;
        camara = cam != null ? cam : Camera.main;
        if (camara == null) return Apuntando;
        if (!Apuntando && !fovCambiado)
        {
            fovNormal = camara.fieldOfView; // sigue los cambios de Opciones
            return false;
        }
        fovCambiado = true;
        float objetivo = fovNormal / (Apuntando ? Zoom : 1f);
        camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, objetivo, 1f - Mathf.Exp(-3f * Time.deltaTime / Tiempo));
        if (!Apuntando && Mathf.Abs(camara.fieldOfView - fovNormal) < 0.05f)
        {
            camara.fieldOfView = fovNormal;
            fovCambiado = false;
        }
        return Apuntando;
    }

    /// <summary>Al guardar el arma: deja de apuntar y vuelve el campo de visión en el momento.</summary>
    public void Soltar()
    {
        Apuntando = false;
        if (fovCambiado && camara != null) camara.fieldOfView = fovNormal;
        fovCambiado = false;
    }
}
