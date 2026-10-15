using TMPro;
using UnityEngine;

// US 120 · Un botón del panel del campo de práctica (PanelDePractica): se usa mirándolo de cerca y apretando la tecla de
// Plantar (E). Mientras se lo mira se ilumina.
public class BotonDePractica : MonoBehaviour
{
    public enum Accion { MoverBots, Reiniciar }

    public Accion accion;
    public TextMeshPro texto;
    public Material normal, resaltado;

    private Renderer fondo;

    private void Awake() => fondo = GetComponent<Renderer>();

    public void Resaltar(bool si)
    {
        if (fondo != null && normal != null && resaltado != null) fondo.sharedMaterial = si ? resaltado : normal;
    }
}
