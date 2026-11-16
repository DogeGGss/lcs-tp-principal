using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;

// US 282, CA3: sin una sesión de cuenta, un build publicado no deja jugar. Esta pantalla tapa el menú (y el nombre de
// jugador y la nota para el PO) hasta que se entre en el launcher: «Abrir el launcher» lo abre y cierra el juego.
// Diseño en la guía, #cuenta-juego. Si la sesión se cerró con el juego abierto (desde el launcher, o se borró la
// cuenta), dice «Tu sesión se cerró». MenuRiftwalker la arma en su propio lienzo, arriba de todo.
public class PantallaSinSesion
{
    public const string GuiaLauncher = "https://dogeggss.github.io/lcs-tp-principal/#launcher";
    private readonly GameObject lienzo;
    private readonly TextMeshProUGUI rotulo, texto, nota;
    private int pintada = -1;

    public PantallaSinSesion(RectTransform lienzo, RectTransform raiz)
    {
        this.lienzo = lienzo.gameObject;
        Image(Stretch(Node("Fondo", lienzo)), null, new Color(0.03f, 0.035f, 0.045f, 1f), 0f, true);
        lienzo.Find("Contenido")?.SetAsLastSibling();
        rotulo = T(raiz, "Rotulo", 150f, 250f, 900f, 26f, FRotulo, 22f, Accent, "", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(raiz, "Titulo", 144f, 284f, 1600f, 110f, FTitulo, 104f, Ink, "Iniciá sesión en el launcher", TextAlignmentOptions.MidlineLeft, 1f, true);
        texto = T(raiz, "Texto", 150f, 410f, 1100f, 90f, FTexto, 30f, Soft, "", TextAlignmentOptions.TopLeft);
        texto.textWrappingMode = TextWrappingModes.Normal;
        string[] pasos = { "Abrí el launcher", "Entrá con tu cuenta", "Tocá Jugar" };
        for (int i = 0; i < pasos.Length; i++)
        {
            RectTransform p = Place(Node("Paso" + i, raiz), 150f + i * 360f, 540f, 340f, 84f);
            Image(p, Rounded, PanelColor, 8f);
            Borde(p, 8f, White(0.1f));
            T(p, "Numero", 24f, 0f, 50f, 84f, FTitulo, 48f, Accent, (i + 1).ToString());
            T(p, "Texto", 74f, 0f, 260f, 84f, FTexto, 26f, Ink, pasos[i]);
        }
        CrearBoton(raiz, 150f, 680f, 380f, 76f, "Abrir el launcher", true, Abrir, 30f);
        CrearBoton(raiz, 550f, 680f, 220f, 76f, "Salir", false, Application.Quit, 30f);
        nota = T(raiz, "Nota", 152f, 784f, 1200f, 30f, FTexto, 21f, Mute, "Si el launcher no está en esta PC, bajalo desde la guía del juego.");
    }

    /// <summary>desplazada: se entró con la cuenta en otra computadora (US 393, CA2).</summary>
    public void Mostrar(bool ver, bool cerrada, bool desplazada = false)
    {
        if (lienzo.activeSelf != ver) lienzo.SetActive(ver);
        int estado = desplazada ? 2 : cerrada ? 1 : 0;
        if (!ver || pintada == estado) return;
        pintada = estado;
        rotulo.text = desplazada ? L10n.Source("Tu cuenta se abrió en otra computadora", "profile.")
            : cerrada ? L10n.Source("Tu sesión se cerró", "profile.") : "Project Riftwalker";
        rotulo.color = cerrada || desplazada ? Bad : Accent;
        texto.text = L10n.Source(desplazada
            ? "Alguien entró con tu cuenta en otra computadora y acá se cerró la sesión: una cuenta se juega en una sola computadora a la vez. Para jugar acá, volvé a entrar en el launcher. Tu progreso está guardado en la cuenta."
            : cerrada
            ? "Se cerró la sesión de tu cuenta, por ejemplo desde el launcher. Volvé a entrar ahí para seguir jugando: tu progreso está guardado en la cuenta."
            : "Para jugar hace falta una cuenta. Abrí el launcher, entrá con tu cuenta y tocá Jugar: el juego se abre con ella.", "profile.");
    }

    private void Abrir()
    {
        if (AbrirLauncher()) return;
        nota.text = L10n.T("profile.session.no_launcher");
        Application.OpenURL(GuiaLauncher);
    }

    /// <summary>Abre el launcher de la instalación y cierra el juego. false si no está (el juego no se instaló con el
    /// launcher). También lo usa buscar partida, con una versión vieja.</summary>
    public static bool AbrirLauncher()
    {
        // El launcher está en la carpeta de la instalación, arriba de la del juego (game/Project Riftwalker.exe).
        string juego = Path.GetDirectoryName(Application.dataPath);
        string instalacion = juego != null ? Path.GetDirectoryName(juego) : null;
        string launcher = instalacion != null ? Path.Combine(instalacion, "Riftwalker-Launcher.exe") : null;
        if (launcher == null || !File.Exists(launcher)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = instalacion, UseShellExecute = true });
            Application.Quit();
            return true;
        }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning("No se pudo abrir el launcher: " + e.Message); return false; }
    }
}
