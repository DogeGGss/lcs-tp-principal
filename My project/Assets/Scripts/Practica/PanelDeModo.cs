using TMPro;
using UnityEngine;

// US 166, CA3 · Panel de un modo (Táctico, Deathmatch o Zombie) en la pared izquierda de la zona de salida del campo de
// práctica (lo pone el menú Riftwalker > Mapa de práctica). De lejos se ve solo el nombre del modo; al acercarse
// muestra el objetivo, cómo se gana y cómo se pierde. Los textos se editan en el Inspector; "{Plantar}" (o el nombre de
// cualquier otra acción de KeyBindings entre llaves) se cambia por la tecla que configuró el jugador. Cerca del panel,
// la misma explicación sale también en pantalla (TutorialDePractica).
public class PanelDeModo : MonoBehaviour
{
    public TextMeshPro titulo, detalle, acercate;
    [TextArea(2, 6)] public string objetivo;
    [TextArea(1, 4)] public string comoSeGana;
    [TextArea(1, 4)] public string comoSePierde;
    [Tooltip("A cuántos metros muestra el detalle.")]
    public float alcance = 4f;

    private static readonly Color Amarillo = new Color(1f, 0.82f, 0.1f);

    private Transform jugador;
    private bool cerca;

    private void OnEnable()
    {
        KeyBindings.Changed += Escribir;
        Escribir();
        Mostrar(false);
    }

    private void OnDisable() => KeyBindings.Changed -= Escribir;

    private void Update()
    {
        if (jugador == null)
        {
            PlayerMovement p = FindAnyObjectByType<PlayerMovement>();
            if (p == null) return;
            jugador = p.transform;
        }
        Vector3 a = jugador.position - transform.position;
        a.y = 0f;
        bool ahora = a.magnitude <= alcance;
        if (ahora != cerca) Mostrar(ahora);
    }

    private void Mostrar(bool si)
    {
        cerca = si;
        if (detalle != null) detalle.gameObject.SetActive(si);
        if (acercate != null) acercate.gameObject.SetActive(!si);
        TutorialDePractica pantalla = TutorialDePractica.Actual;
        if (pantalla == null) return;
        if (si) pantalla.MostrarModo(this, Nombre, Explicacion());
        else pantalla.OcultarModo(this);
    }

    private void Escribir()
    {
        if (detalle != null) detalle.text = Explicacion();
        if (cerca && TutorialDePractica.Actual != null)
            TutorialDePractica.Actual.MostrarModo(this, Nombre, Explicacion());
    }

    public string Nombre => titulo != null ? titulo.text : name;

    public string Explicacion()
    {
        string amarillo = ColorUtility.ToHtmlStringRGB(Amarillo);
        return $"<color=#{amarillo}>OBJETIVO</color>\n{Teclas(objetivo)}\n\n" +
               $"<color=#{amarillo}>CÓMO SE GANA</color>\n{Teclas(comoSeGana)}\n\n" +
               $"<color=#{amarillo}>CÓMO SE PIERDE</color>\n{Teclas(comoSePierde)}";
    }

    // "{Plantar}" → la tecla de esa acción, resaltada.
    private static string Teclas(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        foreach (GameAction accion in KeyBindings.All)
            texto = texto.Replace("{" + accion + "}", $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{KeyBindings.Label(accion)}</color>");
        return texto;
    }
}
