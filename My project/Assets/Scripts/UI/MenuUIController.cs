using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;

public class MenuUIController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelMenuPrincipal;
    [SerializeField] private GameObject panelSeleccionModo;
    [SerializeField] private GameObject panelSala;
    [SerializeField] private GameObject panelZombie;
    [SerializeField] private GameObject panelLogros;
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private GameObject panelDeseaSalir;
    [SerializeField] private GameObject panelGraficos;
[SerializeField] private GameObject panelControles;
[SerializeField] private GameObject panelSonido;

    [Header("Transición")]
    [SerializeField] private CanvasGroup overlayTransicion;
    [SerializeField] private float duracionTransicion = 0.4f;
[SerializeField] private float duracionEntradaPanel = 0.3f;

    private void Start()
    {
        OcultarTodosLosPaneles();

        panelMenuPrincipal.SetActive(true);

        overlayTransicion.alpha = 0f;
        overlayTransicion.blocksRaycasts = false;
    }

public void IrAlEscenario()
{
    SceneManager.LoadScene("TestScene_Agustinn");
}

    public void MostrarMenuPrincipal()
    {
        CambiarPanel(panelMenuPrincipal);
    }

    public void MostrarSeleccionModo()
    {
        CambiarPanel(panelSeleccionModo);
    }

    public void MostrarSala()
    {
        CambiarPanel(panelSala);
    }

    public void MostrarZombie()
    {
        CambiarPanel(panelZombie);
    }

    public void MostrarLogros()
    {
        CambiarPanel(panelLogros);
    }

    public void MostrarOpciones()
    {
        CambiarPanel(panelOpciones);
    }

   

    public void MostrarGraficos()
{
    panelGraficos.SetActive(true);
    panelControles.SetActive(false);
    panelSonido.SetActive(false);
}

public void MostrarControles()
{
    panelGraficos.SetActive(false);
    panelControles.SetActive(true);
    panelSonido.SetActive(false);
}

public void MostrarSonido()
{
    panelGraficos.SetActive(false);
    panelControles.SetActive(false);
    panelSonido.SetActive(true);
}

   public void SalirJuego()
{
    panelDeseaSalir.SetActive(true);
}

public void CancelarSalida()
{
    panelDeseaSalir.SetActive(false);
}

public void ConfirmarSalida()
{
    Application.Quit();
}

    private void CambiarPanel(GameObject nuevoPanel)
    {
        StopAllCoroutines();
        StartCoroutine(Transicionar(nuevoPanel));
    }

   private IEnumerator Transicionar(GameObject nuevoPanel)
{
    overlayTransicion.blocksRaycasts = true;

    // Oscurecer la pantalla
    yield return StartCoroutine(Fade(0f, 1f));

    // Cambiar panel
    OcultarTodosLosPaneles();
    nuevoPanel.SetActive(true);

    // Buscar el Canvas Group del nuevo panel
    CanvasGroup canvasGroup = nuevoPanel.GetComponent<CanvasGroup>();

    if (canvasGroup != null)
    {
        canvasGroup.alpha = 0f;
    }

    // Sacar el fade negro
    yield return StartCoroutine(Fade(1f, 0f));

    // Mostrar suavemente el contenido del panel
    if (canvasGroup != null)
    {
        yield return StartCoroutine(FadePanel(canvasGroup, 0f, 1f));
    }

    overlayTransicion.blocksRaycasts = false;
}

    private IEnumerator Fade(float inicio, float fin)
    {
        float tiempo = 0f;

        while (tiempo < duracionTransicion)
        {
            tiempo += Time.unscaledDeltaTime;

            float porcentaje = tiempo / duracionTransicion;

            overlayTransicion.alpha =
                Mathf.Lerp(inicio, fin, porcentaje);

            yield return null;
        }

        overlayTransicion.alpha = fin;
    }

    private IEnumerator FadePanel(CanvasGroup canvasGroup, float inicio, float fin)
{
    float tiempo = 0f;

    while (tiempo < duracionEntradaPanel)
    {
        tiempo += Time.unscaledDeltaTime;

        float porcentaje = tiempo / duracionEntradaPanel;

        canvasGroup.alpha =
            Mathf.Lerp(inicio, fin, porcentaje);

        yield return null;
    }

    canvasGroup.alpha = fin;
}

    private void OcultarTodosLosPaneles()
    {
        panelMenuPrincipal.SetActive(false);
        panelSeleccionModo.SetActive(false);
        panelSala.SetActive(false);
        panelZombie.SetActive(false);
        panelLogros.SetActive(false);
        panelOpciones.SetActive(false);
         panelGraficos.SetActive(false);
    panelControles.SetActive(false);
    panelSonido.SetActive(false);
    }
}