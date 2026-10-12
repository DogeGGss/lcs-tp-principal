using UnityEngine;
using UnityEngine.UI;

// Small deterministic UI mesh: no textures, post-processing or particle dependencies.
public sealed class RiftwalkerRiftGraphic : MaskableGraphic
{
    private float time;
    public void SetTime(float value) { time = value; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float grow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 1.05f, time));
        float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.05f, 2.35f, time));
        float alpha = grow * (1 - Mathf.InverseLerp(1.8f, 2.65f, time));
        if (alpha <= 0) return;
        float halfHeight = 195 * grow;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 previous = new Vector2(side * 615 * open, -halfHeight);
            for (int i = 1; i <= 16; i++)
            {
                float y = Mathf.Lerp(-halfHeight, halfHeight, i / 16f);
                float jitter = Mathf.Sin(i * 7.31f) * 13 + Mathf.Sin(i * 2.17f) * 7;
                var next = new Vector2(side * (615 * open + jitter * Mathf.Sin(i * Mathf.PI / 16)), y);
                Segment(mesh, previous, next, 25, new Color(1f, 0.22f, 0.015f, alpha * 0.06f));
                Segment(mesh, previous, next, 10, new Color(1f, 0.39f, 0.05f, alpha * 0.18f));
                Segment(mesh, previous, next, 3, new Color(1f, 0.62f, 0.2f, alpha));
                Segment(mesh, previous, next, 1, new Color(1f, 0.93f, 0.72f, alpha));
                previous = next;
            }
        }
        float burst = Mathf.Clamp01((time - 0.85f) / 1.4f);
        for (int i = 0; i < 28; i++)
        {
            float angle = i * 2.39996f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f);
            var p = direction * (35 + burst * (180 + (i % 5) * 43));
            Segment(mesh, p, p + direction * (4 + 12 * burst), 1.5f,
                new Color(1f, 0.56f, 0.14f, alpha * Mathf.Sin(burst * Mathf.PI)));
        }
    }

    private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        var normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
        int index = mesh.currentVertCount;
        mesh.AddVert(a - normal, color, Vector2.zero);
        mesh.AddVert(a + normal, color, Vector2.zero);
        mesh.AddVert(b + normal, color, Vector2.zero);
        mesh.AddVert(b - normal, color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
