using UnityEngine;
using static ShopUIKit;
using static PerfilUI;

// US 278 (CA4): el logo de cada rango del nivel de jugador, dentro del hexágono de la cabecera del Perfil. Son cosas del
// tren, dibujadas con figuras (sin imágenes), del color del rango:
// - Pasajero: un boleto.        - Guarda: la gorra de guarda.
// - Maquinista: la rueda de la locomotora.        - Jefe de estación: el reloj del andén con una estrella.
// Coordenadas desde arriba a la izquierda del padre, con el centro del logo en (cx, cy); entra en un radio de unos 32.
public static class EmblemaDeNivel
{
    private static readonly Color Fondo = Hex(0x0B0E14);

    /// <summary>Dibuja el logo del rango (0 Pasajero, 1 Guarda, 2 Maquinista, 3 Jefe de estación).</summary>
    public static void Dibujar(RectTransform padre, float cx, float cy, int rango, Color color)
    {
        switch (rango)
        {
            case 0: Boleto(padre, cx, cy, color); break;
            case 1: Gorra(padre, cx, cy, color); break;
            case 2: Rueda(padre, cx, cy, color); break;
            default: Reloj(padre, cx, cy, color); break;
        }
    }

    public static int RangoDe(int nivel) => nivel >= 1250 ? 3 : nivel >= 1100 ? 2 : nivel >= 950 ? 1 : 0;

    // Pasajero: un boleto con las muescas a los costados y la línea de corte punteada.
    private static void Boleto(RectTransform p, float cx, float cy, Color c)
    {
        Rect(p, cx, cy, 52f, 32f, c);
        Circulo(p, cx - 26f, cy, 7f, Fondo);
        Circulo(p, cx + 26f, cy, 7f, Fondo);
        for (int i = 0; i < 4; i++) Rect(p, cx + 9f, cy - 12f + i * 8f, 3f, 4f, Fondo);
        Rect(p, cx - 6f, cy - 6f, 16f, 3f, Fondo);
        Rect(p, cx - 8f, cy + 2f, 12f, 3f, Fondo);
    }

    // Guarda: la gorra, con la copa, la banda, la visera y la insignia.
    private static void Gorra(RectTransform p, float cx, float cy, Color c)
    {
        const int pasos = 12;
        var copa = new Vector2[pasos + 1];
        for (int i = 0; i <= pasos; i++)
        {
            float a = Mathf.PI * i / pasos; // de izquierda (180°) a derecha (0°), por arriba
            copa[i] = new Vector2(cx - Mathf.Cos(a) * 24f, cy + 4f - Mathf.Sin(a) * 20f);
        }
        Poligono(p, "Copa", c, copa);
        Rect(p, cx, cy + 3f, 48f, 6f, Color.Lerp(c, Color.black, 0.45f)); // la banda
        Poligono(p, "Visera", c, new Vector2(cx - 26f, cy + 7f), new Vector2(cx + 26f, cy + 7f),
            new Vector2(cx + 30f, cy + 15f), new Vector2(cx - 30f, cy + 15f));
        Circulo(p, cx, cy - 6f, 5f, Ink);
        Circulo(p, cx, cy - 6f, 2.5f, c);
    }

    // Maquinista: la rueda de la locomotora, con el aro, los rayos y el eje.
    private static void Rueda(RectTransform p, float cx, float cy, Color c)
    {
        Circulo(p, cx, cy, 26f, c, 6f);
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.PI * 2f * i / 8f;
            Linea(p, new Vector2(cx, cy), new Vector2(cx + Mathf.Cos(a) * 22f, cy + Mathf.Sin(a) * 22f), 3.5f, c);
        }
        Circulo(p, cx, cy, 8f, c);
        Circulo(p, cx, cy, 3.5f, Fondo);
        Circulo(p, cx + 12f, cy + 6f, 4f, Ink); // la manivela de la biela
    }

    // Jefe de estación: el reloj del andén (con las agujas y las marcas) y una estrella arriba.
    private static void Reloj(RectTransform p, float cx, float cy, Color c)
    {
        float y = cy + 4f;
        Circulo(p, cx, y, 23f, c, 5f);
        for (int i = 0; i < 12; i += 3)
        {
            float a = Mathf.PI * 2f * i / 12f;
            Linea(p, new Vector2(cx + Mathf.Sin(a) * 13f, y - Mathf.Cos(a) * 13f), new Vector2(cx + Mathf.Sin(a) * 17f, y - Mathf.Cos(a) * 17f), 2.5f, Ink);
        }
        Linea(p, new Vector2(cx, y), new Vector2(cx, y - 13f), 3f, Ink);
        Linea(p, new Vector2(cx, y), new Vector2(cx + 9f, y + 4f), 3f, Ink);
        Circulo(p, cx, y, 3f, c);
        // La estrella: dos triángulos.
        float sy = cy - 26f, r = 8f;
        Poligono(p, "Estrella1", c, new Vector2(cx, sy - r), new Vector2(cx + r * 0.87f, sy + r * 0.5f), new Vector2(cx - r * 0.87f, sy + r * 0.5f));
        Poligono(p, "Estrella2", c, new Vector2(cx, sy + r), new Vector2(cx - r * 0.87f, sy - r * 0.5f), new Vector2(cx + r * 0.87f, sy - r * 0.5f));
    }

    // ---------- Figuras ----------

    private static void Rect(RectTransform p, float cx, float cy, float w, float h, Color c) =>
        Poligono(p, "Rect", c, new Vector2(cx - w / 2f, cy - h / 2f), new Vector2(cx + w / 2f, cy - h / 2f),
            new Vector2(cx + w / 2f, cy + h / 2f), new Vector2(cx - w / 2f, cy + h / 2f));

    private static void Linea(RectTransform p, Vector2 a, Vector2 b, float ancho, Color c)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * (ancho / 2f);
        Poligono(p, "Linea", c, a + n, b + n, b - n, a - n);
    }
}
