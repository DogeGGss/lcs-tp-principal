using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// US 019 · Trasbordo, lo que se ve y se oye (CA8 y CA10). Lo usan el jugador que lo hace y las copias de los demás
// (JugadorEnRed), así todos ven lo mismo: un destello donde estaba y otro donde aparece, y el sonido donde estaba.
// Todo se arma por código para no depender de prefabs: una luz corta, un aro en el piso y una columna de luz.
public static class EfectosTrasbordo
{
    public static readonly Color Tono = new Color(0.45f, 0.85f, 1f);
    public const float AlcanceSonido = 25f; // CA10

    private static Material material;
    private static AudioClip generado;

    /// <param name="jugador">El que lo usó (para la altura de los pies y el grupo de sonido).</param>
    /// <param name="propio">Es el jugador de esta computadora: ve el efecto de pantalla y oye el sonido sin distancia.</param>
    public static void Reproducir(Vector3 origen, Vector3 destino, Transform jugador, bool propio, AudioMixerGroup grupo = null)
    {
        float pies = Pies(jugador);
        Destello(origen + Vector3.up * pies, 1f);
        Destello(destino + Vector3.up * pies, 0.8f);
        Sonido(origen, propio, grupo != null ? grupo : Grupo(jugador));
        if (propio) Pantalla();
    }

    // Cuánto más abajo que el centro del jugador están sus pies.
    private static float Pies(Transform jugador)
    {
        CharacterController cuerpo = jugador != null ? jugador.GetComponent<CharacterController>() : null;
        if (cuerpo == null) return -1f;
        return (cuerpo.center.y - cuerpo.height * 0.5f) * jugador.lossyScale.y;
    }

    private static AudioMixerGroup Grupo(Transform jugador)
    {
        if (jugador == null) return null;
        Pistola pistola = jugador.GetComponentInChildren<Pistola>(true);
        return pistola != null ? pistola.sfxGroup : null;
    }

    // ---------- Destello ----------

    private static void Destello(Vector3 pies, float fuerza)
    {
        var go = new GameObject("Trasbordo (destello)");
        go.transform.position = pies;
        go.AddComponent<DestelloTrasbordo>().Iniciar(MaterialLineas(), fuerza);
    }

    private static Material MaterialLineas()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader) { name = "Trasbordo (líneas)" };
        }
        return material;
    }

    // ---------- Sonido ----------

    private static void Sonido(Vector3 lugar, bool propio, AudioMixerGroup grupo)
    {
        ConfigRed config = ConfigRed.Actual;
        AudioClip clip = config != null && config.sonidoTrasbordo != null ? config.sonidoTrasbordo : Generado();
        var go = new GameObject("Trasbordo (sonido)");
        go.transform.position = lugar;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.outputAudioMixerGroup = grupo; // SFX (CA10)
        fuente.spatialBlend = propio ? 0f : 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = AlcanceSonido;
        fuente.dopplerLevel = 0f;
        fuente.clip = clip;
        fuente.volume = config != null ? config.volumenTrasbordo : 0.8f;
        fuente.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    // Un "zip" que baja de tono sobre un soplido: así hay sonido aunque todavía no se haya cargado un clip en ConfigRed.
    private static AudioClip Generado()
    {
        if (generado != null) return generado;
        const int frecuencia = 44100;
        const float duracion = 0.32f;
        int n = Mathf.CeilToInt(frecuencia * duracion);
        var datos = new float[n];
        var azar = new System.Random(19);
        float fase = 0f, ruido = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)frecuencia, k = t / duracion;
            float envolvente = Mathf.Min(1f, t / 0.012f) * Mathf.Pow(1f - k, 2.2f);
            float hz = Mathf.Lerp(1500f, 260f, Mathf.Sqrt(k));
            fase += 2f * Mathf.PI * hz / frecuencia;
            float blanco = (float)azar.NextDouble() * 2f - 1f;
            ruido += (blanco - ruido) * Mathf.Lerp(0.6f, 0.08f, k); // el soplido se va oscureciendo
            datos[i] = envolvente * (0.45f * Mathf.Sin(fase) + 0.55f * ruido);
        }
        generado = AudioClip.Create("Trasbordo (generado)", n, 1, frecuencia, false);
        generado.SetData(datos, 0);
        return generado;
    }

    // ---------- Pantalla (solo el que lo usa) ----------

    private static void Pantalla()
    {
        var go = new GameObject("Trasbordo (pantalla)");
        Canvas lienzo = go.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 500;
        var imagen = new GameObject("Destello").AddComponent<Image>();
        imagen.transform.SetParent(go.transform, false);
        imagen.raycastTarget = false;
        RectTransform rect = imagen.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.AddComponent<PantallaTrasbordo>().Iniciar(imagen);
    }
}
