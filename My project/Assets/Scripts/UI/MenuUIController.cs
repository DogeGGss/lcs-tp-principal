using UnityEngine;

public class MenuUIController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelMenuPrincipal;
    [SerializeField] private GameObject panelSeleccionModo;
    [SerializeField] private GameObject panelSala;
    [SerializeField] private GameObject panelZombie;
    [SerializeField] private GameObject panelDificultad;
    [SerializeField] private GameObject panelLogros;
    [SerializeField] private GameObject panelOpciones;
    private void Start()
    {
        MostrarMenuPrincipal();
    }

    public void MostrarMenuPrincipal()
    {
        OcultarTodosLosPaneles();

        panelMenuPrincipal.SetActive(true);
    }

    public void MostrarSeleccionModo()
    {
        OcultarTodosLosPaneles();

        panelSeleccionModo.SetActive(true);
    }

    public void MostrarSala()
    {
        OcultarTodosLosPaneles();

        panelSala.SetActive(true);
    }

    public void MostrarZombie()
    {
        OcultarTodosLosPaneles();

        panelZombie.SetActive(true);
    }

     public void MostrarDificultad()
    {
        OcultarTodosLosPaneles();
        panelDificultad.SetActive(true);
    }

    public void MostrarLogros()
{
    OcultarTodosLosPaneles();
    panelLogros.SetActive(true);
}

public void MostrarOpciones()
{
    OcultarTodosLosPaneles();
    panelOpciones.SetActive(true);
}

    public void SalirJuego()
    {
        Application.Quit();
    }

    private void OcultarTodosLosPaneles()
{
    panelMenuPrincipal.SetActive(false);
    panelSeleccionModo.SetActive(false);
    panelSala.SetActive(false);
    panelZombie.SetActive(false);
    panelLogros.SetActive(false);
    panelOpciones.SetActive(false);
    panelDificultad.SetActive(false);
}
}