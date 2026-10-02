using System.Collections;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

// Modo "Sin bordes" de las opciones de video (US 153, CA1): una ventana del tamaño de la resolución elegida, sin
// barra de título ni marco, centrada en el monitor. No obliga a ocupar toda la pantalla: eso es "Completa".
// Unity no tiene este modo, así que se le sacan los bordes a la ventana con la API de Windows. Solo funciona en la
// build de Windows; en el editor y en otras plataformas queda como una ventana común.
public class VentanaSinBordes : MonoBehaviour
{
    private static VentanaSinBordes instancia;
    private static bool sinBordes;

    /// <summary>Después de pasar a modo ventana con Screen.SetResolution: le saca el marco a la ventana.</summary>
    public static void Quitar(int ancho, int alto)
    {
        if (instancia == null)
        {
            var go = new GameObject("VentanaSinBordes");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<VentanaSinBordes>();
        }
        instancia.StopAllCoroutines();
        instancia.StartCoroutine(instancia.QuitarDespues(ancho, alto));
    }

    /// <summary>Antes de pasar a "Ventana" o "Completa": le devuelve el marco, si se lo habían sacado.</summary>
    public static void Restaurar()
    {
        if (instancia != null) instancia.StopAllCoroutines();
        if (!sinBordes) return;
        CambiarBordes(false, 0, 0);
        sinBordes = false;
    }

    // Unity cambia la ventana uno o dos cuadros después de Screen.SetResolution: se espera a que termine.
    private IEnumerator QuitarDespues(int ancho, int alto)
    {
        for (int i = 0; i < 3; i++) yield return null;
        CambiarBordes(true, ancho, alto);
        sinBordes = true;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L, WS_SYSMENU = 0x00080000L,
        WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L;
    private const long Marco = WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_FRAMECHANGED = 0x0020,
        SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static IntPtr ventana = IntPtr.Zero;

    private static void CambiarBordes(bool quitar, int ancho, int alto)
    {
        if (ventana == IntPtr.Zero) ventana = GetActiveWindow();
        if (ventana == IntPtr.Zero) return; // el juego no tiene el foco: se deja como ventana común

        long estilo = GetWindowLongPtr(ventana, GWL_STYLE).ToInt64();
        estilo = quitar ? estilo & ~Marco : estilo | Marco;
        SetWindowLongPtr(ventana, GWL_STYLE, new IntPtr(estilo));

        if (quitar)
        {
            // Sin marco, la ventana mide justo la resolución elegida y va centrada en el monitor.
            int x = Mathf.Max(0, (Display.main.systemWidth - ancho) / 2);
            int y = Mathf.Max(0, (Display.main.systemHeight - alto) / 2);
            SetWindowPos(ventana, IntPtr.Zero, x, y, ancho, alto, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }
        else
            SetWindowPos(ventana, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }
#else
    private static void CambiarBordes(bool quitar, int ancho, int alto) { }
#endif
}
