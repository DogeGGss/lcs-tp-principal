using UnityEngine;

// Configuración de la mira del HUD (US 172). Se guarda en PlayerPrefs, como la sensibilidad y el campo de visión,
// y la leen la mira del HUD (CombatHud) y la pestaña "Mira" de las opciones (MiraUIController).
public static class MiraConfig
{
    public const int Cruz = 0, Circulo = 1, Punto = 2;
    public const int ColorPropio = 5;
    public const float LargoMin = 2f, LargoMax = 20f, GrosorMin = 1f, GrosorMax = 6f, HuecoMin = 0f, HuecoMax = 15f;

    public static readonly string[] NombresTipo = { "Cruz", "Círculo", "Punto" };

    // CA2: blanco, verde, amarillo, celeste y rojo.
    public static readonly Color32[] Colores =
    {
        new Color32(255, 255, 255, 255), new Color32(61, 220, 151, 255), new Color32(255, 216, 74, 255),
        new Color32(88, 214, 255, 255), new Color32(255, 92, 92, 255)
    };

    public struct Mira
    {
        public int tipo;                   // Cruz, Circulo o Punto
        public float largo, grosor, hueco; // px en 1920 x 1080
        public bool punto, borde, dinamica;
        public int color;                  // 0 a 4: Colores; 5: propio
        public Color32 propio;

        public Color Color => color >= 0 && color < Colores.Length ? (Color)Colores[color] : (Color)propio;

        public bool IgualA(Mira o) =>
            tipo == o.tipo && Mathf.Approximately(largo, o.largo) && Mathf.Approximately(grosor, o.grosor) &&
            Mathf.Approximately(hueco, o.hueco) && punto == o.punto && borde == o.borde && dinamica == o.dinamica &&
            color == o.color && propio.r == o.propio.r && propio.g == o.propio.g && propio.b == o.propio.b;
    }

    /// <summary>CA6: la mira fija de la US 171 (cuatro rayitas blancas con borde y un hueco en el centro).</summary>
    public static Mira Defecto => new Mira
    {
        tipo = Cruz, largo = 8f, grosor = 2f, hueco = 5f, punto = false, borde = true, dinamica = false,
        color = 0, propio = new Color32(61, 220, 151, 255)
    };

    private static bool leida;
    private static Mira actual;

    /// <summary>Avisa a la mira del HUD cuando se guarda una configuración nueva.</summary>
    public static event System.Action Cambiada;

    /// <summary>CA5: lo último guardado (o la mira por defecto).</summary>
    public static Mira Actual
    {
        get
        {
            if (!leida) { actual = Leer(); leida = true; }
            return actual;
        }
    }

    private static Mira Leer()
    {
        Mira d = Defecto;
        return new Mira
        {
            tipo = Mathf.Clamp(PlayerPrefs.GetInt("Mira_Tipo", d.tipo), 0, NombresTipo.Length - 1),
            largo = Mathf.Clamp(PlayerPrefs.GetFloat("Mira_Largo", d.largo), LargoMin, LargoMax),
            grosor = Mathf.Clamp(PlayerPrefs.GetFloat("Mira_Grosor", d.grosor), GrosorMin, GrosorMax),
            hueco = Mathf.Clamp(PlayerPrefs.GetFloat("Mira_Hueco", d.hueco), HuecoMin, HuecoMax),
            punto = PlayerPrefs.GetInt("Mira_Punto", d.punto ? 1 : 0) == 1,
            borde = PlayerPrefs.GetInt("Mira_Borde", d.borde ? 1 : 0) == 1,
            dinamica = PlayerPrefs.GetInt("Mira_Dinamica", d.dinamica ? 1 : 0) == 1,
            color = Mathf.Clamp(PlayerPrefs.GetInt("Mira_Color", d.color), 0, ColorPropio),
            propio = new Color32(
                (byte)Mathf.Clamp(PlayerPrefs.GetInt("Mira_R", d.propio.r), 0, 255),
                (byte)Mathf.Clamp(PlayerPrefs.GetInt("Mira_G", d.propio.g), 0, 255),
                (byte)Mathf.Clamp(PlayerPrefs.GetInt("Mira_B", d.propio.b), 0, 255), 255)
        };
    }

    public static void Guardar(Mira m)
    {
        PlayerPrefs.SetInt("Mira_Tipo", m.tipo);
        PlayerPrefs.SetFloat("Mira_Largo", m.largo);
        PlayerPrefs.SetFloat("Mira_Grosor", m.grosor);
        PlayerPrefs.SetFloat("Mira_Hueco", m.hueco);
        PlayerPrefs.SetInt("Mira_Punto", m.punto ? 1 : 0);
        PlayerPrefs.SetInt("Mira_Borde", m.borde ? 1 : 0);
        PlayerPrefs.SetInt("Mira_Dinamica", m.dinamica ? 1 : 0);
        PlayerPrefs.SetInt("Mira_Color", m.color);
        PlayerPrefs.SetInt("Mira_R", m.propio.r);
        PlayerPrefs.SetInt("Mira_G", m.propio.g);
        PlayerPrefs.SetInt("Mira_B", m.propio.b);
        PlayerPrefs.Save();
        actual = m;
        leida = true;
        Cambiada?.Invoke();
    }
}
