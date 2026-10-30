using UnityEngine;

// F08 (Modo Invasión), US 148, CA4: la categoría Suministros de la tienda, que solo existe en Invasión (la agrega
// ShopCatalog.ItemsIn). Por ahora tiene la munición: llena la reserva de las dos armas. Cuesta 300 como el resto del
// catálogo, que en Invasión vale la mitad (ShopItem.Price): 150 puntos.
public static class Suministros
{
    private static ShopItem municion;

    public static ShopItem Municion
    {
        get
        {
            if (municion != null) return municion;
            municion = ScriptableObject.CreateInstance<ShopItem>();
            municion.hideFlags = HideFlags.DontSave;
            municion.name = "Municion";
            municion.displayName = "Munición";
            municion.category = ShopCategory.Supplies;
            municion.kind = ShopItemKind.Ammo;
            municion.price = 300;
            municion.tint = ShopUIKit.Accent;
            municion.shortEffect = "Las dos armas";
            municion.description = "Llena el cargador y la reserva de tus dos armas.";
            municion.icon = Icono();
            return municion;
        }
    }

    // Un cargador naranja con dos balas asomando arriba (la opción C que eligió Luka). Se dibuja por código con
    // suavizado (4 × 4 muestras por píxel), en coordenadas de un dibujo de 56 × 108 (y hacia abajo).
    private static Sprite Icono()
    {
        const int ancho = 64, alto = 128, muestras = 4;
        const float escala = 1.12f, margenX = (ancho - 56f * escala) / 2f, margenY = (alto - 108f * escala) / 2f;
        Color cargador = ShopUIKit.Accent, bala = ShopUIKit.Ink;
        var textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixeles = new Color[ancho * alto];
        for (int py = 0; py < alto; py++)
            for (int px = 0; px < ancho; px++)
            {
                int enBala = 0, enCargador = 0;
                for (int sy = 0; sy < muestras; sy++)
                    for (int sx = 0; sx < muestras; sx++)
                    {
                        // Al dibujo: x de 0 a 56, y de 0 (arriba) a 108. La textura tiene la y hacia arriba.
                        float x = (px + (sx + 0.5f) / muestras - margenX) / escala;
                        float y = (alto - (py + (sy + 0.5f) / muestras) - margenY) / escala;
                        if (Bala(x, y, 10f) || Bala(x, y, 26f)) enBala++;
                        else if (Cargador(x, y)) enCargador++;
                    }
                float total = muestras * muestras;
                float a = (enBala + enCargador) / total;
                Color c = a > 0f ? Color.Lerp(cargador, bala, enBala / (float)(enBala + enCargador)) : Color.clear;
                c.a = a;
                pixeles[py * ancho + px] = c;
            }
        textura.SetPixels(pixeles);
        textura.Apply();
        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // El cargador: un trapecio inclinado, de (0,17)-(44,17) arriba a (12,107)-(56,107) abajo.
    private static bool Cargador(float x, float y)
    {
        if (y < 17f || y > 107f) return false;
        float corrido = (y - 17f) / 90f * 12f;
        return x >= corrido && x <= 44f + corrido;
    }

    // Una bala de 12 de ancho que empieza en x: la punta redonda (de y 0 a 7) y la vaina (de 7 a 19).
    private static bool Bala(float x, float y, float desde)
    {
        float dx = x - (desde + 6f);
        if (dx < -6f || dx > 6f) return false;
        if (y >= 7f && y <= 19f) return true;
        if (y < 0f || y > 7f) return false;
        float dy = (7f - y) / 7f;
        return (dx / 6f) * (dx / 6f) + dy * dy <= 1f;
    }
}
