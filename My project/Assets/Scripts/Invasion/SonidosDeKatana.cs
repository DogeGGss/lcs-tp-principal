using UnityEngine;

// US 395, CA9: los sonidos de la katana, generados por código (como el chispazo de la Escolta): el corte del aire (más
// grave y largo en el tajo fuerte), la estocada (un corte largo con un zumbido de energía), el golpe al tocar a alguien
// y el choque metálico al frenar un disparo con la guardia. Si hay grabaciones en Resources/Katana/Sonidos (Corte,
// CorteFuerte, Estocada, Golpe, Freno) se usan esas.
public static class SonidosDeKatana
{
    private const int Frecuencia = 44100;
    private static AudioClip corte, corteFuerte, estocada, golpe, freno;

    public static AudioClip Corte(bool fuerte) => fuerte
        ? Cargar(ref corteFuerte, "CorteFuerte", () => Silbido("KatanaCorteFuerte", 0.38f, 900f, 260f, 0.95f, 0f, 3951))
        : Cargar(ref corte, "Corte", () => Silbido("KatanaCorte", 0.24f, 1500f, 450f, 0.8f, 0f, 3952));

    public static AudioClip Estocada() =>
        Cargar(ref estocada, "Estocada", () => Silbido("KatanaEstocada", 0.45f, 700f, 1800f, 0.9f, 0.35f, 3953));

    public static AudioClip Golpe() => Cargar(ref golpe, "Golpe", CrearGolpe);

    public static AudioClip Freno() => Cargar(ref freno, "Freno", CrearFreno);

    private static AudioClip Cargar(ref AudioClip clip, string nombre, System.Func<AudioClip> generar)
    {
        if (clip != null) return clip;
        clip = Resources.Load<AudioClip>("Katana/Sonidos/" + nombre);
        if (clip == null) clip = generar();
        return clip;
    }

    // Un "fiuu": ruido filtrado cuyo tono va de "desde" a "hasta" Hz, que crece y se apaga. "zumbido" suma un tono
    // eléctrico (la energía del filo).
    private static AudioClip Silbido(string nombre, float dura, float desde, float hasta, float volumen, float zumbido, int semilla)
    {
        int n = (int)(Frecuencia * dura);
        var datos = new float[n];
        var azar = new System.Random(semilla);
        float bajo = 0f, banda = 0f, fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float f = Mathf.Lerp(desde, hasta, k);
            float a = Mathf.Clamp01(2f * Mathf.PI * f / Frecuencia);
            float r = (float)azar.NextDouble() * 2f - 1f;
            bajo += (r - bajo) * a;          // pasa bajos
            banda += (bajo - banda) * a * 0.5f; // y otro, para quedarse con una banda alrededor de f
            float forma = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f)); // crece rápido y se apaga
            fase += 2f * Mathf.PI * (120f + 60f * k) / Frecuencia;
            float tono = zumbido * (Mathf.Sin(fase) * 0.6f + Mathf.Sin(fase * 2.01f) * 0.3f) * forma;
            datos[i] = ((bajo - banda) * 3.2f * forma + tono * 0.35f) * volumen;
        }
        return Crear(nombre, Normalizar(datos, 0.8f * volumen));
    }

    // Al tocar a alguien: un golpe seco y grave con un chasquido corto arriba. 0,2 s.
    private static AudioClip CrearGolpe()
    {
        int n = (int)(Frecuencia * 0.2f);
        var datos = new float[n];
        var azar = new System.Random(3954);
        float fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            fase += 2f * Mathf.PI * Mathf.Lerp(150f, 60f, k) / Frecuencia;
            float grave = Mathf.Sin(fase) * Mathf.Exp(-k * 9f);
            float chasquido = ((float)azar.NextDouble() * 2f - 1f) * Mathf.Exp(-k * 40f);
            datos[i] = grave * 0.8f + chasquido * 0.5f;
        }
        return Crear("KatanaGolpe", Normalizar(datos, 0.75f));
    }

    // Al frenar un disparo: un "clang" metálico (parciales que no son armónicos y se apagan) con un chispazo. 0,6 s.
    private static AudioClip CrearFreno()
    {
        int n = (int)(Frecuencia * 0.6f);
        var datos = new float[n];
        var azar = new System.Random(3955);
        float[] tonos = { 1180f, 2630f, 4070f, 5310f };
        float[] caidas = { 5f, 7f, 10f, 14f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Frecuencia, k = i / (float)n;
            float v = 0f;
            for (int j = 0; j < tonos.Length; j++) v += Mathf.Sin(2f * Mathf.PI * tonos[j] * t) * Mathf.Exp(-k * caidas[j]) / (j + 1f);
            float chispa = ((float)azar.NextDouble() * 2f - 1f) * Mathf.Exp(-k * 30f);
            datos[i] = v * 0.7f + chispa * 0.4f;
        }
        return Crear("KatanaFreno", Normalizar(datos, 0.85f));
    }

    private static float[] Normalizar(float[] datos, float tope)
    {
        float max = 0.0001f;
        foreach (float d in datos) max = Mathf.Max(max, Mathf.Abs(d));
        float k = tope / max;
        for (int i = 0; i < datos.Length; i++) datos[i] *= k;
        return datos;
    }

    private static AudioClip Crear(string nombre, float[] datos)
    {
        AudioClip c = AudioClip.Create(nombre, datos.Length, 1, Frecuencia, false);
        c.SetData(datos, 0);
        return c;
    }
}
