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
    [SerializeField] private GameObject panelCreditos;

    [Header("Pruebas")]
    [Tooltip("Escena que abre el botón PRUEBAS (tiene que estar en Build Settings).")]
    [SerializeField] private string escenaPruebas = "MapaPruebasV1";
    [Tooltip("Escena que abre el botón PRUEBAS 2 (tiene que estar en Build Settings).")]
    [SerializeField] private string escenaPruebas2 = "Example_01 1";
    [Tooltip("Escena del campo de práctica (US 118), la que abre el menú principal (US 122).")]
    [SerializeField] private string escenaPractica = "Mapa_practica";

    [Header("Transición")]
    [SerializeField] private CanvasGroup overlayTransicion;
    [SerializeField] private float duracionTransicion = 0.4f;
[SerializeField] private float duracionEntradaPanel = 0.3f;

    private static bool opcionesCargadas;
    private bool transicionando;
    private GameObject panelDestino;

    // El menú siempre tiene que responder al mouse. A veces quedaba trabado (el puntero no hacía nada y ningún botón
    // respondía), en el juego y en el editor. Las causas posibles se arreglan cada cuadro:
    // - el cursor trabado o escondido (de la partida de la que se volvió);
    // - el velo de la transición entre paneles, que tapa los clics mientras dura: si la transición se cortaba (el menú
    //   se apagaba en el medio), quedaba invisible tapando todo;
    // - sin EventSystem prendido (el que lleva los clics a los botones);
    // - el tiempo frenado (de la pausa de la partida): las animaciones de los botones no avanzaban.
    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
        if (!transicionando && overlayTransicion != null && overlayTransicion.blocksRaycasts)
        {
            overlayTransicion.blocksRaycasts = false;
            overlayTransicion.alpha = 0f;
        }
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
        AsegurarEventSystem();
    }

    private static void AsegurarEventSystem()
    {
        UnityEngine.EventSystems.EventSystem actual = UnityEngine.EventSystems.EventSystem.current;
        if (actual != null && actual.isActiveAndEnabled) return;
        foreach (UnityEngine.EventSystems.EventSystem e in FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (e == null || e.gameObject.scene.name == null) continue; // (un prefab)
            e.gameObject.SetActive(true);
            e.enabled = true;
            UnityEngine.EventSystems.BaseInputModule modulo = e.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
            if (modulo != null) modulo.enabled = true;
            return;
        }
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    // Si el menú se apaga en medio de una transición, la corrutina se corta: se deja todo como al terminar.
    private void OnDisable()
    {
        if (!transicionando) return;
        transicionando = false;
        if (overlayTransicion != null) { overlayTransicion.alpha = 0f; overlayTransicion.blocksRaycasts = false; }
        if (panelDestino != null)
        {
            panelDestino.SetActive(true);
            CanvasGroup grupo = panelDestino.GetComponent<CanvasGroup>();
            if (grupo != null) grupo.alpha = 1f;
        }
    }

    private void LocalizarTextosDePrefabs()
    {
        foreach (var panel in new[] { panelMenuPrincipal,panelSeleccionModo,panelSala,panelZombie,panelLogros,panelOpciones,panelDeseaSalir,panelCreditos })
        {
            if (panel == null) continue;
            foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                var input = text.GetComponentInParent<TMPro.TMP_InputField>(true);
                if (input != null && input.textComponent == text) continue;
                if (L10n.TryAuthoredKey(text.text,out var key))
                    L10n.Bind(text,key);
            }
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                var input = text.GetComponentInParent<UnityEngine.UI.InputField>(true);
                if (input != null && input.textComponent == text) continue;
                if (L10n.TryAuthoredKey(text.text,out var key))
                    L10n.Bind(text,key);
            }
        }
    }

    private void Start()
    {
        LocalizarTextosDePrefabs();
        OcultarTodosLosPaneles();

        // El menú principal lo dibuja MenuRiftwalker (con la versión y "Volver a la partida"); este panel queda vacío y
        // marca que se está en el menú.
        panelMenuPrincipal.SetActive(true);

        overlayTransicion.alpha = 0f;
        overlayTransicion.blocksRaycasts = false;

        // US 048, CA5: las opciones guardadas se usan desde que arranca el juego, sin tener que abrir Opciones.
        if (!opcionesCargadas)
        {
            opcionesCargadas = true;
            GraficosUIController.AplicarGuardado();
        }
        foreach (SonidoUIController sonido in FindObjectsByType<SonidoUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sonido.AplicarGuardado();
    }

public void IrAlEscenario()
{
    SceneManager.LoadScene(escenaPruebas);
}

public void IrAlEscenario2()
{
    SceneManager.LoadScene(escenaPruebas2);
}

    // US 122, CA1 y CA2: el botón "Campo de práctica" del menú lleva directo al mapa de práctica, con la pantalla de
    // carga (US 196) como las partidas. Se vuelve con la pausa (CA3).
    public void IrAlCampoDePractica()
    {
        // La captura del mapa está en Resources/Carga (así no hay que asignarla en la escena del menú).
        PantallaDeCarga.Cargar(escenaPractica, L10n.Source("Campo de práctica", "ui."), L10n.Source("Mapa de práctica", "ui."), Resources.Load<Sprite>("Carga/MapaPractica"));
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

    public void MostrarCreditos()
    {
        CambiarPanel(panelCreditos);
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
#if UNITY_EDITOR
    // En el editor Application.Quit no hace nada: se corta el Play para que se note que salió.
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
}

    private void CambiarPanel(GameObject nuevoPanel)
    {
        StopAllCoroutines();
        StartCoroutine(Transicionar(nuevoPanel));
    }

   private IEnumerator Transicionar(GameObject nuevoPanel)
{
    transicionando = true;
    panelDestino = nuevoPanel;
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
    transicionando = false;
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
        if (panelCreditos != null) panelCreditos.SetActive(false);
    }
}
