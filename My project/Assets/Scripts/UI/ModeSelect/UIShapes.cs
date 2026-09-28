using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Figuras vectoriales para la interfaz: se dibujan con su propia malla, sin sprites.
// Las coordenadas son píxeles desde el pivote del RectTransform, con la y hacia abajo (como en la maqueta).
public abstract class UIShape : MaskableGraphic
{
    protected static Vector2 V(Vector2 p) => new Vector2(p.x, -p.y);

    protected static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 ca, Color32 cb, Color32 cc)
    {
        int i = vh.currentVertCount;
        vh.AddVert(V(a), ca, Vector2.zero);
        vh.AddVert(V(b), cb, Vector2.zero);
        vh.AddVert(V(c), cc, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }

    protected static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
    {
        int i = vh.currentVertCount;
        vh.AddVert(V(a), ca, Vector2.zero);
        vh.AddVert(V(b), cb, Vector2.zero);
        vh.AddVert(V(c), cc, Vector2.zero);
        vh.AddVert(V(d), cd, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    protected static void Disc(VertexHelper vh, Vector2 center, float radius, Color32 color, int segments = 20)
    {
        int start = vh.currentVertCount;
        vh.AddVert(V(center), color, Vector2.zero);
        for (int s = 0; s <= segments; s++)
        {
            float a = s * Mathf.PI * 2f / segments;
            vh.AddVert(V(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius), color, Vector2.zero);
            if (s > 0) vh.AddTriangle(start, start + s, start + s + 1);
        }
    }
}

// Polígono relleno (se triangula por orejas, así acepta formas cóncavas como el zigzag del rayo).
public class UIPolygon : UIShape
{
    private readonly List<Vector2> points = new List<Vector2>();
    private static readonly List<int> triangles = new List<int>(), open = new List<int>();

    public void SetPoints(IList<Vector2> value)
    {
        points.Clear();
        points.AddRange(value);
        SetVerticesDirty();
    }

    public static Vector2[] Rect(float x, float y, float width, float height) =>
        new[] { new Vector2(x, y), new Vector2(x + width, y), new Vector2(x + width, y + height), new Vector2(x, y + height) };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 3) return;
        Color32 c = color;
        foreach (Vector2 p in points) vh.AddVert(V(p), c, Vector2.zero);
        Triangulate(points, triangles);
        for (int i = 0; i + 2 < triangles.Count; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    private static void Triangulate(List<Vector2> p, List<int> result)
    {
        result.Clear();
        open.Clear();
        float area = 0f;
        for (int i = 0; i < p.Count; i++) { open.Add(i); area += Cross(p[i], p[(i + 1) % p.Count]); }
        float sign = area >= 0f ? 1f : -1f;

        int guard = 0;
        while (open.Count > 3 && guard++ < 4096)
        {
            bool clipped = false;
            for (int i = 0; i < open.Count; i++)
            {
                int a = open[(i + open.Count - 1) % open.Count], b = open[i], c = open[(i + 1) % open.Count];
                if (Cross(p[b] - p[a], p[c] - p[b]) * sign <= 0f) continue; // vértice cóncavo
                bool blocked = false;
                foreach (int j in open)
                    if (j != a && j != b && j != c && Inside(p[j], p[a], p[b], p[c])) { blocked = true; break; }
                if (blocked) continue;
                result.Add(a); result.Add(b); result.Add(c);
                open.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        // Lo que quede (puntos alineados o un caso raro) se cierra en abanico.
        for (int i = 1; i + 1 < open.Count; i++) { result.Add(open[0]); result.Add(open[i]); result.Add(open[i + 1]); }
    }
}

// Línea quebrada con grosor, borde suave, degradé horizontal, trazos y dibujado parcial (para la entrada del rayo).
public class UIPolyline : UIShape
{
    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<Vector2> work = new List<Vector2>();
    private readonly List<Vector2> piece = new List<Vector2> { Vector2.zero, Vector2.zero };

    public float thickness = 2f;
    public float feather;              // px de borde que se desvanece por fuera
    public bool closed;
    public bool roundJoins;            // pone un círculo en cada punto (brazos, puntas redondeadas)
    public bool useGradient;           // color → colorB de izquierda a derecha entre gradientX0 y gradientX1
    public Color colorB = Color.white;
    public float gradientX0, gradientX1 = 1f;
    public float dash, gap, dashOffset; // dash = 0: línea continua
    public float drawFraction = 1f;    // parte del largo que se dibuja, desde el primer punto

    public void SetPoints(IList<Vector2> value)
    {
        points.Clear();
        points.AddRange(value);
        SetVerticesDirty();
    }

    public void Refresh() => SetVerticesDirty();

    private Color32 ColorAt(Vector2 p, float alpha)
    {
        Color c = color;
        if (useGradient) c = Color.Lerp(color, colorB, Mathf.InverseLerp(gradientX0, gradientX1, p.x));
        c.a *= alpha;
        return c;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 2) return;

        work.Clear();
        work.AddRange(points);
        if (closed) work.Add(points[0]);
        if (drawFraction < 1f) Trim(work, drawFraction);
        if (work.Count < 2) return;

        if (dash > 0f) DrawDashes(vh);
        else DrawStrip(vh, work);

        if (roundJoins)
            foreach (Vector2 p in work) Disc(vh, p, thickness / 2f, ColorAt(p, 1f), 16);
    }

    private static void Trim(List<Vector2> pts, float fraction)
    {
        float total = 0f;
        for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
        float left = Mathf.Max(0f, fraction) * total;
        for (int i = 1; i < pts.Count; i++)
        {
            float d = Vector2.Distance(pts[i - 1], pts[i]);
            if (left <= d)
            {
                Vector2 end = Vector2.Lerp(pts[i - 1], pts[i], d > 0f ? left / d : 0f);
                pts.RemoveRange(i, pts.Count - i);
                pts.Add(end);
                return;
            }
            left -= d;
        }
    }

    private void DrawDashes(VertexHelper vh)
    {
        // Los trazos se miden sobre el largo total, así siguen de un tramo al otro y avanzan con dashOffset.
        float period = dash + Mathf.Max(0f, gap);
        float start = -Mathf.Repeat(dashOffset, period), d0 = 0f;
        for (int i = 1; i < work.Count; i++)
        {
            Vector2 a = work[i - 1], b = work[i];
            float length = Vector2.Distance(a, b);
            if (length <= 0f) continue;
            int m = Mathf.FloorToInt((d0 - start) / period);
            for (float s = start + m * period; s < d0 + length; s += period)
            {
                float s0 = Mathf.Max(s, d0), s1 = Mathf.Min(s + dash, d0 + length);
                if (s1 <= s0) continue;
                piece[0] = Vector2.Lerp(a, b, (s0 - d0) / length);
                piece[1] = Vector2.Lerp(a, b, (s1 - d0) / length);
                DrawStrip(vh, piece);
            }
            d0 += length;
        }
    }

    private void DrawStrip(VertexHelper vh, List<Vector2> pts)
    {
        float half = thickness / 2f;
        int n = pts.Count;
        Vector2 prevL = default, prevR = default, prevLo = default, prevRo = default;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = pts[i];
            Vector2 dIn = i > 0 ? (p - pts[i - 1]).normalized : Vector2.zero;
            Vector2 dOut = i < n - 1 ? (pts[i + 1] - p).normalized : Vector2.zero;
            Vector2 dir = (dIn + dOut).sqrMagnitude > 1e-6f ? (dIn + dOut).normalized : (dOut != Vector2.zero ? dOut : dIn);
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector2 segNormal = dOut != Vector2.zero ? new Vector2(-dOut.y, dOut.x) : new Vector2(-dIn.y, dIn.x);
            float miter = 1f / Mathf.Max(0.3f, Vector2.Dot(normal, segNormal));
            Vector2 off = normal * half * Mathf.Min(miter, 3f);
            Vector2 offOuter = normal * (half + feather) * Mathf.Min(miter, 3f);

            Vector2 l = p + off, r = p - off, lo = p + offOuter, ro = p - offOuter;
            if (i > 0)
            {
                Vector2 q = pts[i - 1];
                Color32 cq = ColorAt(q, 1f), cp = ColorAt(p, 1f), cq0 = ColorAt(q, 0f), cp0 = ColorAt(p, 0f);
                Quad(vh, prevL, l, r, prevR, cq, cp, cp, cq);
                if (feather > 0f)
                {
                    Quad(vh, prevLo, lo, l, prevL, cq0, cp0, cp, cq);
                    Quad(vh, prevR, r, ro, prevRo, cq, cp, cp0, cq0);
                }
            }
            prevL = l; prevR = r; prevLo = lo; prevRo = ro;
        }
    }
}

// Círculo, aro (con o sin cortes) o brillo redondo que se desvanece hacia afuera.
public class UIRing : UIShape
{
    public float radius = 50f;
    public float thickness;          // 0: círculo lleno
    public int segments = 64;
    public int dashCount;            // 0: aro continuo
    [Range(0f, 1f)] public float dashFill = 0.65f;
    public bool glow;                // lleno con degradé: color en el centro y transparente en el borde
    [Range(0f, 1f)] public float glowMid = 0.3f; // alfa relativo a mitad de radio

    public void Refresh() => SetVerticesDirty();

    private static Vector2 At(float angle, float r) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color32 c = color;
        if (glow)
        {
            // Caída suave en 12 anillos: el alfa a mitad de radio es glowMid (sin escalones visibles).
            const int rings = 12;
            float power = Mathf.Log(Mathf.Clamp(glowMid, 0.01f, 0.99f)) / Mathf.Log(0.5f);
            Color32 Ring(int r) { Color k = color; k.a *= Mathf.Pow(1f - r / (float)rings, power); return k; }
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                Tri(vh, Vector2.zero, At(a0, radius / rings), At(a1, radius / rings), c, Ring(1), Ring(1));
                for (int r = 1; r < rings; r++)
                {
                    float r0 = radius * r / rings, r1 = radius * (r + 1) / rings;
                    Quad(vh, At(a0, r0), At(a0, r1), At(a1, r1), At(a1, r0), Ring(r), Ring(r + 1), Ring(r + 1), Ring(r));
                }
            }
            return;
        }
        if (thickness <= 0f) { Disc(vh, Vector2.zero, radius, c, segments); return; }

        float inner = Mathf.Max(0f, radius - thickness / 2f), outer = radius + thickness / 2f;
        if (dashCount <= 0)
        {
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                Quad(vh, At(a0, inner), At(a0, outer), At(a1, outer), At(a1, inner), c, c, c, c);
            }
            return;
        }
        float step = Mathf.PI * 2f / dashCount, arc = step * dashFill;
        int sub = Mathf.Max(2, segments / dashCount);
        for (int d = 0; d < dashCount; d++)
            for (int s = 0; s < sub; s++)
            {
                float a0 = d * step + arc * s / sub, a1 = d * step + arc * (s + 1) / sub;
                Quad(vh, At(a0, inner), At(a0, outer), At(a1, outer), At(a1, inner), c, c, c, c);
            }
    }
}
