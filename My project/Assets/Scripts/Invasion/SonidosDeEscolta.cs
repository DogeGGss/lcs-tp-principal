using UnityEngine;

// F29, US 394: los sonidos de la Escolta: el zumbido del dron, el rayo del ojo, el chasquido de los arcos y el chispazo
// en cada invasor que tocan. Los tres primeros son grabaciones (Resources/Escolta/Sonidos: Zumbido y Rayo en bucle, y
// Arcos1 a Arcos3, de los que sale uno al azar); si falta alguna, se genera por código (como el zumbido de las naves de
// paso), igual que el chispazo. Todos en 3D y por el grupo de efectos (los volúmenes de Opciones).
public static class SonidosDeEscolta
{
    private const int Frecuencia = 44100;
    private static AudioClip zumbido, rayo, chasquido, chispazo;
    private static AudioClip[] grabados;
    private static UnityEngine.Audio.AudioMixerGroup efectos;
    private static bool efectosBuscados;

    /// <summary>Una fuente 3D de efectos en ese objeto (en bucle si loop), que se escucha hasta alcance metros.</summary>
    public static AudioSource Fuente(GameObject go, AudioClip clip, bool loop, float volumen, float alcance)
    {
        var f = go.AddComponent<AudioSource>();
        f.clip = clip;
        f.loop = loop;
        f.playOnAwake = false;
        f.volume = volumen;
        f.spatialBlend = 1f;
        f.rolloffMode = AudioRolloffMode.Linear;
        f.minDistance = 1.5f;
        f.maxDistance = alcance;
        f.dopplerLevel = 0f;
        f.outputAudioMixerGroup = Efectos();
        return f;
    }

    /// <summary>Un sonido suelto en ese lugar (se borra solo al terminar).</summary>
    public static void En(Vector3 lugar, AudioClip clip, float volumen, float alcance, float tono = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("Sonido de la Escolta");
        go.transform.position = lugar;
        AudioSource f = Fuente(go, clip, false, volumen, alcance);
        f.pitch = tono;
        f.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, tono) + 0.1f);
    }

    private static UnityEngine.Audio.AudioMixerGroup Efectos()
    {
        if (!efectosBuscados || efectos == null)
        {
            efectosBuscados = true;
            Pistola pistola = Object.FindAnyObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) efectos = pistola.sfxGroup; // volúmenes de Opciones
        }
        return efectos;
    }

    // El zumbido del dron: un tono grave con armónicos que late un poco, con un soplido (el propulsor). 2 s en bucle.
    public static AudioClip Zumbido()
    {
        if (zumbido != null) return zumbido;
        zumbido = Resources.Load<AudioClip>("Escolta/Sonidos/Zumbido");
        if (zumbido != null) return zumbido;
        int n = Frecuencia * 2;
        var datos = new float[n];
        var azar = new System.Random(394);
        float soplido = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Frecuencia;
            float r = (float)azar.NextDouble() * 2f - 1f;
            soplido += (r - soplido) * 0.03f;
            float late = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 6f * t); // 12 latidos en 2 s (sin corte al repetirse)
            float tono = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 280f * t) * 0.22f
                       + Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.08f;
            float k = i / (float)n;
            float borde = Mathf.Min(1f, Mathf.Min(k, 1f - k) * 60f);
            datos[i] = (tono * late * 0.35f + soplido * 1.6f * borde) * 0.8f;
        }
        zumbido = Crear("Zumbido de la Escolta (generado)", datos);
        return zumbido;
    }

    // El rayo del ojo: un zumbido eléctrico áspero (una onda en diente de sierra con chisporroteo). 1 s en bucle.
    public static AudioClip Rayo()
    {
        if (rayo != null) return rayo;
        rayo = Resources.Load<AudioClip>("Escolta/Sonidos/Rayo");
        if (rayo != null) return rayo;
        int n = Frecuencia;
        var datos = new float[n];
        var azar = new System.Random(3940);
        float chispa = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Frecuencia;
            float sierra = 2f * ((t * 110f) % 1f) - 1f;               // 110 vueltas en 1 s
            float agudo = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.25f;
            if (azar.NextDouble() < 0.0015) chispa = 1f;              // chasquidos sueltos
            chispa *= 0.993f;
            float ruido = ((float)azar.NextDouble() * 2f - 1f) * chispa;
            float k = i / (float)n;
            float borde = Mathf.Min(1f, Mathf.Min(k, 1f - k) * 80f);
            datos[i] = (sierra * 0.28f + agudo + ruido * 0.5f * borde) * (0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * 9f * t)) * 0.55f;
        }
        rayo = Crear("Rayo de la Escolta (generado)", datos);
        return rayo;
    }

    // Los arcos: un chasquido eléctrico fuerte (ruido en ráfagas que se apaga, con un tono que baja). 0,5 s.
    public static AudioClip Chasquido()
    {
        if (grabados == null)
        {
            var lista = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 3; i++)
            {
                AudioClip c = Resources.Load<AudioClip>("Escolta/Sonidos/Arcos" + i);
                if (c != null) lista.Add(c);
            }
            grabados = lista.ToArray();
        }
        if (grabados.Length > 0) return grabados[Random.Range(0, grabados.Length)]; // uno de los tres, al azar
        if (chasquido != null) return chasquido;
        int n = (int)(Frecuencia * 0.5f);
        var datos = new float[n];
        var azar = new System.Random(39400);
        float fase = 0f, rafaga = 1f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            if (azar.NextDouble() < 0.004) rafaga = 0.4f + (float)azar.NextDouble() * 0.6f;
            rafaga = Mathf.Max(0.15f, rafaga * 0.9995f);
            float ruido = ((float)azar.NextDouble() * 2f - 1f) * rafaga;
            fase += 2f * Mathf.PI * Mathf.Lerp(1400f, 300f, k) / Frecuencia;
            float caida = Mathf.Exp(-k * 5f);
            float ataque = Mathf.Min(1f, i / (Frecuencia * 0.003f));
            datos[i] = (ruido * 0.75f + Mathf.Sin(fase) * 0.3f) * caida * ataque * 0.8f;
        }
        chasquido = Crear("Arcos de la Escolta (generado)", datos);
        return chasquido;
    }

    // Donde pega cada arco: un chispazo corto. 0,15 s.
    public static AudioClip Chispazo()
    {
        if (chispazo != null) return chispazo;
        int n = (int)(Frecuencia * 0.15f);
        var datos = new float[n];
        var azar = new System.Random(394000);
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float ruido = (float)azar.NextDouble() * 2f - 1f;
            datos[i] = ruido * Mathf.Exp(-k * 9f) * Mathf.Min(1f, i / (Frecuencia * 0.002f)) * 0.6f;
        }
        chispazo = Crear("Chispazo de la Escolta (generado)", datos);
        return chispazo;
    }

    private static AudioClip Crear(string nombre, float[] datos)
    {
        AudioClip c = AudioClip.Create(nombre, datos.Length, 1, Frecuencia, false);
        c.SetData(datos, 0);
        return c;
    }
}
