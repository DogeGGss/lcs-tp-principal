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

    // Una caja de balas naranja (como el ícono de Suministros de la guía de diseño): el borde y tres renglones.
    private static Sprite Icono()
    {
        const int ancho = 84, alto = 34;
        var textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        Color32 nada = new Color32(0, 0, 0, 0), color = ShopUIKit.Accent;
        var pixeles = new Color32[ancho * alto];
        for (int i = 0; i < pixeles.Length; i++) pixeles[i] = nada;
        void Rect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    pixeles[y * ancho + x] = color;
        }
        // Borde de la caja (28 × 22, desde arriba a 6 px), de 3 px. En la textura y crece hacia arriba.
        int izq = 28, der = 56, arriba = alto - 6, abajo = alto - 28;
        Rect(izq, abajo, der, abajo + 3);
        Rect(izq, arriba - 3, der, arriba);
        Rect(izq, abajo, izq + 3, arriba);
        Rect(der - 3, abajo, der, arriba);
        // Los renglones (balas).
        Rect(34, alto - 13, 50, alto - 11);
        Rect(34, alto - 19, 50, alto - 17);
        Rect(34, alto - 25, 44, alto - 23);
        textura.SetPixels32(pixeles);
        textura.Apply();
        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
