using UnityEngine;

// Las plumas que suelta un pájaro del minijuego al recibir un disparo (F21, US 156, CA5): unos rombos chicos que
// salen para los costados, caen despacio girando y se achican hasta desaparecer.
public class Plumas : MonoBehaviour
{
    private const int Cantidad = 8;
    private const float Duracion = 1.3f;

    private static Mesh rombo;

    private Transform[] plumas;
    private Vector3[] velocidades, giros;
    private float tamano, desde;

    public static void Soltar(Vector3 punto, Color color, Color otro, float envergadura, Transform padre)
    {
        var go = new GameObject("Plumas");
        go.transform.SetParent(padre, false);
        go.transform.position = punto;
        var plumas = go.AddComponent<Plumas>();
        plumas.tamano = envergadura * 0.14f;
        plumas.desde = Time.time;
        plumas.plumas = new Transform[Cantidad];
        plumas.velocidades = new Vector3[Cantidad];
        plumas.giros = new Vector3[Cantidad];
        for (int i = 0; i < Cantidad; i++)
        {
            var pluma = new GameObject("Pluma", typeof(MeshFilter), typeof(MeshRenderer));
            pluma.transform.SetParent(go.transform, false);
            pluma.transform.localRotation = Random.rotation;
            pluma.transform.localScale = Vector3.one * plumas.tamano;
            pluma.GetComponent<MeshFilter>().sharedMesh = Rombo();
            var render = pluma.GetComponent<MeshRenderer>();
            render.sharedMaterial = Pajaro.MaterialDe(i % 3 == 0 ? otro : color);
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plumas.plumas[i] = pluma.transform;
            plumas.velocidades[i] = Random.insideUnitSphere * 3.5f + Vector3.up * 1.5f;
            plumas.giros[i] = Random.insideUnitSphere * 540f;
        }
    }

    private void Update()
    {
        float t = (Time.time - desde) / Duracion;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float dt = Time.deltaTime;
        for (int i = 0; i < Cantidad; i++)
        {
            // Caen despacio, como una pluma: poca gravedad y mucho freno.
            velocidades[i] += Physics.gravity * (0.12f * dt);
            velocidades[i] *= 1f - Mathf.Min(1f, 2.2f * dt);
            plumas[i].position += velocidades[i] * dt;
            plumas[i].Rotate(giros[i] * dt, Space.Self);
            plumas[i].localScale = Vector3.one * (tamano * (1f - t * t));
        }
    }

    // Un rombo alargado de las dos caras, compartido por todas las plumas.
    private static Mesh Rombo()
    {
        if (rombo != null) return rombo;
        Vector3 a = new Vector3(0f, 0f, 0.5f), b = new Vector3(0.18f, 0f, 0f), c = new Vector3(0f, 0f, -0.5f), d = new Vector3(-0.18f, 0f, 0f);
        rombo = new Mesh
        {
            name = "Pluma",
            vertices = new[] { a, b, c, a, c, d, a, c, b, a, d, c },
            triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
        };
        rombo.RecalculateNormals();
        rombo.RecalculateBounds();
        return rombo;
    }
}
