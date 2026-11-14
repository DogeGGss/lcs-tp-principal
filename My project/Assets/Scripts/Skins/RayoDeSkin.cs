using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas), US 381: el rayo de la skin legendaria "Tormenta" (EfectosDeSkin): una línea en zigzag del color
// del rayo, de la boca del arma hasta donde pegó la bala, con alguna rama. Cambia de forma en cada tiro, parpadea
// (se vuelve a dibujar con otro zigzag dos o tres veces) y se apaga en un instante. También los arquitos chicos (donde
// pega, y en la boca después de disparar). Es solo visual: no cambia la bala.
public class RayoDeSkin : MonoBehaviour
{
    private const float DistanciaMinima = 0.01f;

    private readonly List<LineRenderer> lineas = new List<LineRenderer>();
    private Vector3 desde, hasta;
    private float dura, empezo, amplitud, proximo;
    private int ramas, tramos;
    private Color color;

    /// <summary>Un rayo de "desde" a "hasta". grosor: en la boca (m); en la punta, más ancho (se ve de lejos).
    /// ramas: cuántas ramas chicas salen de él. dura: s.</summary>
    public static void Crear(Vector3 desde, Vector3 hasta, CatalogoDeSkins.Efectos e, float grosor, float grosorFinal,
                             int ramas, float dura)
    {
        float distancia = Vector3.Distance(desde, hasta);
        if (e == null || distancia < DistanciaMinima) return;
        Material m = ModelosDeArmas.Actual.MaterialDe("efecto-rayo");
        if (m == null) return;
        var go = new GameObject("Rayo de la skin");
        RayoDeSkin r = go.AddComponent<RayoDeSkin>();
        r.desde = desde;
        r.hasta = hasta;
        r.dura = dura;
        r.empezo = Time.time;
        r.ramas = ramas;
        r.color = Color.Lerp(e.fuego, e.nucleo, 0.3f);
        r.tramos = Mathf.Clamp(Mathf.RoundToInt(distancia / 0.6f), 4, 26);
        r.amplitud = Mathf.Clamp(distancia * 0.03f, 0.008f, 0.5f);
        for (int i = 0; i <= ramas; i++)
        {
            LineRenderer l = new GameObject(i == 0 ? "Rayo" : "Rama").AddComponent<LineRenderer>();
            l.transform.SetParent(go.transform, false);
            l.sharedMaterial = m;
            l.useWorldSpace = true;
            l.numCapVertices = 2;
            l.numCornerVertices = 1;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.startWidth = i == 0 ? grosor : grosor * 0.6f;
            l.endWidth = i == 0 ? grosorFinal : grosor * 0.3f;
            r.lineas.Add(l);
        }
        r.Dibujar();
        Destroy(go, dura + 0.05f);
    }

    // Un zigzag nuevo (otro parpadeo): puntos de "desde" a "hasta" corridos al azar de costado, más cerca de la mitad;
    // las ramas salen de un punto del rayo hacia un costado y adelante.
    private void Dibujar()
    {
        Vector3 tramo = hasta - desde;
        Vector3 adelante = tramo.normalized;
        Vector3 lado1 = Vector3.Cross(adelante, Mathf.Abs(adelante.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
        Vector3 lado2 = Vector3.Cross(adelante, lado1);
        var puntos = new Vector3[tramos + 1];
        for (int i = 0; i <= tramos; i++)
        {
            float f = (float)i / tramos;
            float cuanto = i == 0 || i == tramos ? 0f : Mathf.Sin(f * Mathf.PI) * 0.7f + 0.3f;
            Vector2 c = Random.insideUnitCircle * amplitud * cuanto;
            puntos[i] = desde + tramo * f + lado1 * c.x + lado2 * c.y;
        }
        LineRenderer rayo = lineas[0];
        rayo.positionCount = puntos.Length;
        rayo.SetPositions(puntos);
        for (int k = 1; k < lineas.Count; k++)
        {
            int i = Random.Range(1, Mathf.Max(2, tramos - 1));
            Vector3 de = puntos[i];
            Vector3 hacia = (adelante + (lado1 * Random.Range(-1f, 1f) + lado2 * Random.Range(-1f, 1f)) * 0.9f).normalized;
            float largo = tramo.magnitude / tramos * Random.Range(1.5f, 3f);
            var rama = new Vector3[4];
            for (int j = 0; j < rama.Length; j++)
            {
                float f = (float)j / (rama.Length - 1);
                rama[j] = de + hacia * largo * f + (j == 0 ? Vector3.zero : (Vector3)Random.insideUnitCircle * amplitud * 0.4f);
            }
            lineas[k].positionCount = rama.Length;
            lineas[k].SetPositions(rama);
        }
        proximo = Time.time + dura * Random.Range(0.25f, 0.4f);
    }

    private void Update()
    {
        float t = (Time.time - empezo) / Mathf.Max(0.01f, dura);
        if (Time.time >= proximo && t < 0.75f) Dibujar(); // parpadea: otro zigzag
        // Prende fuerte, titila y se apaga.
        float titila = 0.75f + 0.25f * Mathf.Sin(Time.time * 90f);
        float alfa = Mathf.Clamp01(1f - t) * titila;
        Color c = color;
        c.a = alfa;
        foreach (LineRenderer l in lineas) l.startColor = l.endColor = c;
    }
}
