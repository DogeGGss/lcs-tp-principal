using System.Collections.Generic;
using UnityEngine;

// Modo trailer: F1 oculta toda la interfaz de la pantalla (HUD, mira, minimapa, carteles y menús) para grabar tomas
// limpias, y F1 de nuevo la vuelve a mostrar. Apaga cada Canvas de pantalla que esté prendido, también los que se
// crean mientras está activo, y al salir prende solo los que apagó él. Los Canvas en el mundo y los efectos de pantalla
// (el encandilado de la flash y el gris del humo) quedan como están, porque son parte de lo que se ve al jugar.
// F2 esconde además el arma y los brazos de primera persona (las cámaras de las armas del jugador de esta computadora):
// con F1 y F2 la pantalla queda como una cámara limpia, para filmar a otro jugador desde otra computadora o instancia.
public class ModoTrailer : MonoBehaviour
{
    public static bool Activo { get; private set; }
    public static bool SinArma { get; private set; }

    private readonly HashSet<Canvas> apagados = new HashSet<Canvas>();
    private readonly HashSet<Camera> camarasApagadas = new HashSet<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        var objeto = new GameObject("ModoTrailer");
        DontDestroyOnLoad(objeto);
        objeto.AddComponent<ModoTrailer>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Activo = !Activo;
            if (!Activo) Mostrar();
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SinArma = !SinArma;
            if (!SinArma) MostrarArma();
        }
    }

    private void LateUpdate()
    {
        if (SinArma) EsconderArma();
        if (!Activo) return;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.enabled || canvas.renderMode == RenderMode.WorldSpace || EsEfecto(canvas)) continue;
            canvas.enabled = false;
            apagados.Add(canvas);
        }
    }

    // Las cámaras del jugador que no son la principal dibujan el arma y los brazos de primera persona.
    private void EsconderArma()
    {
        Camera principal = Camera.main;
        PlayerMovement jugador = principal != null ? principal.GetComponentInParent<PlayerMovement>() : null;
        if (jugador == null) return;
        foreach (Camera camara in jugador.GetComponentsInChildren<Camera>())
        {
            if (camara == principal || !camara.enabled) continue;
            camara.enabled = false;
            camarasApagadas.Add(camara);
        }
    }

    private void MostrarArma()
    {
        foreach (Camera camara in camarasApagadas)
            if (camara != null) camara.enabled = true;
        camarasApagadas.Clear();
    }

    private static bool EsEfecto(Canvas canvas)
    {
        foreach (MonoBehaviour componente in canvas.GetComponents<MonoBehaviour>())
            if (componente is FlashBlind || componente.GetType().Name == "PantallaDeHumo") return true;
        return false;
    }

    private void Mostrar()
    {
        foreach (Canvas canvas in apagados)
            if (canvas != null) canvas.enabled = true;
        apagados.Clear();
    }
}
