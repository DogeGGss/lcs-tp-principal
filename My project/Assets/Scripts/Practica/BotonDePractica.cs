using TMPro;
using UnityEngine;

// US 120 · Un botón del panel del campo de práctica (PanelDePractica): se usa mirándolo de cerca y apretando la tecla de
// Plantar (E), o pegándole un tiro (WeaponFire). Se ilumina mientras se lo mira y si es el modo elegido.
public class BotonDePractica : MonoBehaviour
{
    public enum Accion { Quietos, Movimiento, Distancias, Reaccion, Reiniciar, Tutorial } // Tutorial: US 166

    public Accion accion;
    public TextMeshPro texto;
    public Material normal, resaltado;

    private const float Espera = 0.3f; // así los perdigones de un mismo tiro lo apretan una sola vez

    private Renderer fondo;
    private PanelDePractica panel;
    private float libreDesde;

    private void Awake()
    {
        fondo = GetComponent<Renderer>();
        panel = GetComponentInParent<PanelDePractica>();
    }

    public void Presionar()
    {
        if (Time.time < libreDesde || panel == null) return;
        libreDesde = Time.time + Espera;
        panel.Presionar(accion);
    }

    // Un disparo que pegó en "collider": si es un botón, lo aprieta. Devuelve si era un botón.
    public static bool Disparar(Collider collider)
    {
        BotonDePractica boton = collider != null ? collider.GetComponentInParent<BotonDePractica>() : null;
        if (boton == null) return false;
        boton.Presionar();
        return true;
    }

    public void Resaltar(bool si)
    {
        if (fondo != null && normal != null && resaltado != null) fondo.sharedMaterial = si ? resaltado : normal;
    }
}
