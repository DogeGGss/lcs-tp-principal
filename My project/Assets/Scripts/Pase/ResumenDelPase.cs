using TMPro;
using UnityEngine;
using static ShopUIKit;

// F32 (US 362, CA3, y US 365, CA4): en la pantalla del final de la partida, la experiencia ganada (de la partida y de los
// desafíos completados), la barra del nivel llenándose y, si se subió, "¡Nivel N!" con lo que se desbloqueó.
public static class ResumenDelPase
{
    public const float Ancho = 440f;

    /// <summary>Dibuja el resumen en (x, y) de root. Devuelve el alto que ocupó.</summary>
    public static float Dibujar(RectTransform root, float x, float y, PaseDeBatalla.Resumen r, TMP_FontAsset display, TMP_FontAsset label, Sprite redondeado)
    {
        if (r == null) return 0f;
        int lineas = Mathf.Min(3, r.desafios.Count) + (r.nivelDespues > r.nivelAntes ? 1 : 0);
        float alto = 118f + lineas * 22f;
        RectTransform caja = Place(Node("PaseDeBatalla", root), x, y, Ancho, alto);
        Image(caja, redondeado, Rgb(10, 12, 17, DarkAlpha(0.88f)), 8f);
        Image(Place(Node("Linea", caja), 0f, 0f, 3f, alto), null, Accent);

        Text(Place(Node("Rotulo", caja), 20f, 12f, 260f, 20f), label, 14f, Accent, TextAlignmentOptions.MidlineLeft, 14f, true).text =
            L10n.Source("Pase de batalla", "profile.") + " · " + PaseDeBatalla.NombreTemporada;
        Text(Place(Node("Xp", caja), 20f, 32f, Ancho - 40f, 36f), display, 32f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text =
            $"+{PerfilDatos.Numero(r.XpTotal)} XP" + (r.xpDesafios > 0
                ? $"  <size=55%><color=#8E96A3>{PerfilDatos.Numero(r.xpPartida)} {L10n.Source("de la partida", "profile.")} · {PerfilDatos.Numero(r.xpDesafios)} {L10n.Source("de desafíos", "profile.")}</color></size>"
                : "");

        // La barra: arranca donde estaba y se llena hasta donde quedó (si se subió de nivel, se llena y vuelve a empezar).
        int nivel = r.nivelDespues;
        bool tope = nivel >= PaseDeBatalla.Niveles;
        int xpDespues = r.xpAntes + r.XpTotal;
        float desde = r.nivelDespues > r.nivelAntes ? 0f : (r.xpAntes % PaseDeBatalla.XpPorNivel) / (float)PaseDeBatalla.XpPorNivel;
        float hasta = tope ? 1f : (xpDespues % PaseDeBatalla.XpPorNivel) / (float)PaseDeBatalla.XpPorNivel;
        Text(Place(Node("Nivel", caja), 20f, 74f, 120f, 22f), label, 15f, Soft, TextAlignmentOptions.MidlineLeft, 10f, true).text =
            L10n.Source("Nivel", "profile.") + " " + nivel;
        Text(Place(Node("Cuenta", caja), Ancho - 180f, 74f, 160f, 22f), label, 15f, Mute, TextAlignmentOptions.MidlineRight, 4f).text =
            tope ? L10n.Source("Nivel máximo", "profile.") : $"{PerfilDatos.Numero(xpDespues % PaseDeBatalla.XpPorNivel)} / {PerfilDatos.Numero(PaseDeBatalla.XpPorNivel)}";
        RectTransform barra = Place(Node("Barra", caja), 20f, 100f, Ancho - 40f, 8f);
        Image(barra, redondeado, White(0.1f), 4f);
        RectTransform relleno = Place(Node("Relleno", barra), 0f, 0f, (Ancho - 40f) * desde, 8f);
        Image(relleno, redondeado, Accent, 4f);
        var anim = relleno.gameObject.AddComponent<BarraQueSeLlena>();
        anim.ancho = Ancho - 40f; anim.desde = desde; anim.hasta = hasta;

        float ly = 116f;
        if (r.nivelDespues > r.nivelAntes)
        {
            string premio = r.desbloqueados.Count > 0 ? r.desbloqueados[0].Nombre : L10n.Source("una recompensa con el pase", "profile.");
            Text(Place(Node("SubioDeNivel", caja), 20f, ly, Ancho - 40f, 22f), display, 20f, Accent, TextAlignmentOptions.MidlineLeft, 2f, true).text =
                $"¡{L10n.Source("Nivel", "profile.")} {r.nivelDespues}!  <color=#D6DAE0><size=80%>{premio}</size></color>";
            ly += 22f;
        }
        for (int i = 0; i < Mathf.Min(3, r.desafios.Count); i++, ly += 22f)
            Text(Place(Node("Desafio" + i, caja), 20f, ly, Ancho - 40f, 22f), label, 15f, Ok, TextAlignmentOptions.MidlineLeft, 2f).text = L10n.Source("Desafío", "profile.") + ": " + r.desafios[i];
        return alto;
    }

    private sealed class BarraQueSeLlena : MonoBehaviour
    {
        public float ancho, desde, hasta;
        private float t;

        private void Update()
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 1.2f);
            float k = 1f - (1f - t) * (1f - t);
            ((RectTransform)transform).sizeDelta = new Vector2(ancho * Mathf.Lerp(desde, hasta, k), ((RectTransform)transform).sizeDelta.y);
            if (t >= 1f) enabled = false;
        }
    }
}
