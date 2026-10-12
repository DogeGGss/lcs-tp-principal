using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Conexiones entre los módulos centrales (Conector_1, 2 y 3), como en las fotos de la uni:
// no es un puente de ladrillo sino una estructura de caños blancos entre las dos puntas de los módulos,
// con una pasarela de rejilla a la altura del primer piso, una escalera pegada a un módulo,
// techo de policarbonato en bóveda, una malla arriba con los equipos de aire y ripio con una vereda abajo.
// Se puede volver a correr: borra lo que armó antes y lo arma de nuevo.
// Coordenadas locales de cada conector: x cruza de un módulo al otro, z sigue el paso, y para arriba.
public static class ConectoresUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Conectores.asset";
    const string Raiz = "Ambiente_ModulosCentrales";
    static readonly string[] Conectores = { "Conector_1", "Conector_2", "Conector_3" };
    static readonly string[] Viejos = { "Ladrillo", "Pilares", "Blancos", "Escalera" };

    const float Z0 = -4.8f, Z1 = 4.8f;      // frente y fondo de la estructura
    const float Piso = 3.35f;               // pasarela, a la altura del primer piso
    const float Arriba = 7.35f;             // marco de arriba, a la altura de los techos
    const float Pasarela = 1.8f;            // profundidad de la pasarela del frente
    const float Lateral = 1.45f;            // ancho de la pasarela pegada al módulo de la derecha
    const float FinLateral = 2.6f;          // hasta dónde llega esa pasarela (antes de la salida de la galería)
    const float MurosMacizos = 2.9f;        // las puntas de los módulos son macizas hasta |z| ≈ 3,4; después, galería
    const float AnchoEscalera = 1.0f;
    const int Escalones = 18;
    const float Huella = 0.26f;
    const float Columna = 1.6f;             // columnas a los lados de la vereda
    const float Vereda = 0.95f;             // media vereda de hormigón

    class Batch
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int> t = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public Material mat;

        // Cara con el frente hacia "afuera"; la textura va en metros.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 afuera, bool planta = false)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), afuera) < 0) { var x = b; b = d; d = x; }
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            if (planta) uv.AddRange(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z) });
            else
            {
                float w = Vector3.Distance(a, b), h = Vector3.Distance(b, c);
                uv.AddRange(new[] { Vector2.zero, new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
            }
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public void DobleCara(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool planta = false)
        {
            var n = Vector3.Cross(b - a, c - a);
            Quad(a, b, c, d, n, planta);
            Quad(a, b, c, d, -n, planta);
        }
    }

    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
    static Transform conector, marco, colisiones;
    static float l0, lp, r0, rp; // muro izquierdo en x = -(l0 + lp z), derecho en x = r0 + rp z

    static Batch Get(string material)
    {
        if (!batches.TryGetValue(material, out var b))
        {
            b = new Batch { mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + material + ".mat") };
            if (b.mat == null) throw new InvalidOperationException("Falta el material Uni_" + material);
            batches.Add(material, b);
        }
        return b;
    }

    static float XL(float z) => -(l0 + lp * z);
    static float XR(float z) => r0 + rp * z;
    static Vector3 PL(float z, float y, float adentro) => new Vector3(XL(z) + adentro, y, z);
    static Vector3 PR(float z, float y, float adentro) => new Vector3(XR(z) - adentro, y, z);

    // Caja de 8 esquinas: abajo a0..a3 y arriba b0..b3, en el mismo orden.
    static void Caja(string mat, Vector3[] a, Vector3[] b)
    {
        var batch = Get(mat);
        var centro = (a.Aggregate(Vector3.zero, (s, p) => s + p) + b.Aggregate(Vector3.zero, (s, p) => s + p)) / 8;
        void Cara(Vector3 p, Vector3 q, Vector3 r, Vector3 s) => batch.Quad(p, q, r, s, (p + q + r + s) / 4 - centro);
        Cara(a[0], a[1], a[2], a[3]);
        Cara(b[0], b[1], b[2], b[3]);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Cara(a[i], a[j], b[j], b[i]);
        }
    }

    // Perfil rectangular de a hasta b (ancho de costado y alto).
    static void Barra(string mat, Vector3 a, Vector3 b, float ancho, float alto)
    {
        var dir = (b - a).normalized;
        var referencia = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
        var costado = Vector3.Cross(referencia, dir).normalized * (ancho / 2);
        var arriba = Vector3.Cross(dir, costado).normalized * (alto / 2);
        Caja(mat,
            new[] { a - costado - arriba, a + costado - arriba, a + costado + arriba, a - costado + arriba },
            new[] { b - costado - arriba, b + costado - arriba, b + costado + arriba, b - costado + arriba });
    }

    // Caño redondo (prisma de pocos lados) de a hasta b.
    static void Cano(string mat, Vector3 a, Vector3 b, float radio, int lados = 8)
    {
        var batch = Get(mat);
        var dir = (b - a).normalized;
        var referencia = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
        var u = Vector3.Cross(referencia, dir).normalized;
        var w = Vector3.Cross(dir, u).normalized;
        for (int i = 0; i < lados; i++)
        {
            float a0 = i * Mathf.PI * 2 / lados, a1 = (i + 1) * Mathf.PI * 2 / lados;
            var o0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * radio;
            var o1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * radio;
            batch.Quad(a + o0, a + o1, b + o1, b + o0, (o0 + o1) / 2);
        }
    }

    static void Colision(string nombre, Vector3 centro, Vector3 medida, Quaternion giro)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(colisiones, false);
        go.transform.localPosition = centro;
        go.transform.localRotation = giro;
        go.AddComponent<BoxCollider>().size = medida;
        GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
    }

    // Colisión de una barra de a hasta b (para barandas, columnas y la rampa de la escalera).
    static void ColisionEntre(string nombre, Vector3 a, Vector3 b, float ancho, float alto)
    {
        var dir = b - a;
        var giro = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
        Colision(nombre, (a + b) / 2, new Vector3(ancho, alto, dir.magnitude), giro);
    }

    static void PonerColumna(Vector3 pie, float alto)
    {
        Cano("PinturaBlanca", pie, pie + Vector3.up * alto, 0.07f);
        Cano("ChapaOxido", pie, pie + Vector3.up * 0.18f, 0.074f); // el óxido del pie, como en las fotos
        ColisionEntre("Columna", pie, pie + Vector3.up * alto, 0.16f, 0.16f);
    }

    // Baranda entre dos puntos del piso: pasamanos, travesaño, parantes cada ~1,2 m.
    static void Baranda(Vector3 a, Vector3 b, bool colision = true)
    {
        float largo = Vector3.Distance(a, b);
        int tramos = Mathf.Max(1, Mathf.CeilToInt(largo / 1.2f));
        Cano("PinturaBlanca", a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, 0.025f, 6);
        Cano("PinturaBlanca", a + Vector3.up * 0.5f, b + Vector3.up * 0.5f, 0.02f, 6);
        for (int i = 0; i <= tramos; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)tramos);
            Cano("PinturaBlanca", p, p + Vector3.up * 1.0f, 0.022f, 6);
        }
        if (colision) ColisionEntre("Baranda", a + Vector3.up * 0.55f, b + Vector3.up * 0.55f, 0.08f, 1.1f);
    }

    // Piso de rejilla entre cuatro esquinas (en sentido horario visto desde arriba), con su marco de perfiles.
    static void Rejilla(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var e = Vector3.up * 0.04f;
        Get("PortonMalla").DobleCara(a + e, b + e, c + e, d + e, true);
        foreach (var (p, q) in new[] { (a, b), (b, c), (c, d), (d, a) })
            Barra("PinturaBlanca", p - Vector3.up * 0.06f, q - Vector3.up * 0.06f, 0.08f, 0.2f);
    }

    static void Aire(Vector3 enMuro, Vector3 normal, Vector3 aLoLargo, float ancho = 0.82f, float alto = 0.56f)
    {
        // Equipo de aire acondicionado partido, colgado del muro.
        var h = Vector3.up * alto / 2;
        var l = aLoLargo.normalized * ancho / 2;
        var f = normal.normalized * 0.30f;
        Caja("PosteBlanco", new[] { enMuro - l - h, enMuro + l - h, enMuro + l - h + f, enMuro - l - h + f },
                            new[] { enMuro - l + h, enMuro + l + h, enMuro + l + h + f, enMuro - l + h + f });
        var c = enMuro + f + normal.normalized * 0.005f - aLoLargo.normalized * ancho * 0.12f;
        var r = alto * 0.36f;
        var batch = Get("MetalNegro");
        for (int i = 0; i < 8; i++)
        {
            float a0 = i * Mathf.PI / 4, a1 = (i + 1) * Mathf.PI / 4;
            var o0 = aLoLargo.normalized * Mathf.Cos(a0) * r + Vector3.up * Mathf.Sin(a0) * r;
            var o1 = aLoLargo.normalized * Mathf.Cos(a1) * r + Vector3.up * Mathf.Sin(a1) * r;
            batch.Quad(c, c + o0, c + o1, c, normal);
        }
    }

    static void Armar(Transform c)
    {
        conector = c;
        // La estructura va girada media vuelta respecto del conector: así la pasarela queda del lado de afuera
        // (hacia el Módulo 7) y, mirando desde la entrada, la escalera sube por el módulo de la derecha hacia el fondo.
        // Todo lo de abajo está en las coordenadas de la estructura.
        var est = new GameObject("Estructura");
        Undo.RegisterCreatedObjectUndo(est, "Estructura del conector");
        est.transform.SetParent(c, false);
        est.transform.localRotation = Quaternion.Euler(0, 180, 0);
        marco = est.transform;
        colisiones = new GameObject("Colisiones").transform;
        colisiones.SetParent(est.transform, false);

        // Muros de las puntas de los dos módulos (están en ángulo: el paso se abre hacia un lado).
        float Distancia(float z, int lado)
        {
            var o = marco.TransformPoint(new Vector3(0, 5f, z));
            if (!Physics.Raycast(o, marco.TransformDirection(new Vector3(lado, 0, 0)), out var h, 20f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException(c.name + ": no se encontró el muro del módulo.");
            return h.distance;
        }
        float la = Distancia(-3, -1), lb = Distancia(3, -1), ra = Distancia(-3, 1), rb = Distancia(3, 1);
        l0 = (la + lb) / 2; lp = (lb - la) / 6; r0 = (ra + rb) / 2; rp = (rb - ra) / 6;

        // ---------- Suelo: ripio con una vereda de hormigón por el medio ----------
        // El ripio sigue al terreno (en el conector 3 hay un lomo de casi 20 cm) para que no asome el pasto.
        var ripio = Get("Ripio");
        var terreno = Terrain.activeTerrain;
        Vector3 Ripio(float t, float z)
        {
            var p = Vector3.Lerp(PL(z, 0, 0), PR(z, 0, 0), t);
            if (terreno != null) p.y = terreno.SampleHeight(marco.TransformPoint(p)) + terreno.transform.position.y - marco.position.y;
            p.y = Mathf.Max(p.y + 0.03f, 0.05f);
            return p;
        }
        const int celdasZ = 22, celdasX = 26;
        for (int k = 0; k < celdasZ; k++)
            for (int i = 0; i < celdasX; i++)
            {
                float za = Mathf.Lerp(Z0 - 0.4f, Z1 + 0.4f, k / (float)celdasZ), zb = Mathf.Lerp(Z0 - 0.4f, Z1 + 0.4f, (k + 1) / (float)celdasZ);
                float ta = i / (float)celdasX, tb = (i + 1) / (float)celdasX;
                ripio.Quad(Ripio(ta, za), Ripio(tb, za), Ripio(tb, zb), Ripio(ta, zb), Vector3.up, true);
            }
        Caja("Vereda",
            new[] { new Vector3(-Vereda, 0, Z0 - 0.4f), new Vector3(Vereda, 0, Z0 - 0.4f), new Vector3(Vereda, 0, Z1 + 0.4f), new Vector3(-Vereda, 0, Z1 + 0.4f) },
            new[] { new Vector3(-Vereda, 0.19f, Z0 - 0.4f), new Vector3(Vereda, 0.19f, Z0 - 0.4f), new Vector3(Vereda, 0.19f, Z1 + 0.4f), new Vector3(-Vereda, 0.19f, Z1 + 0.4f) });
        Colision("Vereda", new Vector3(0, 0.095f, 0), new Vector3(Vereda * 2, 0.19f, Z1 - Z0 + 0.8f), Quaternion.identity);

        // ---------- Columnas de caño blanco, del piso hasta el marco de arriba ----------
        // Las de los muros van sobre la parte maciza (|z| < 3): más allá están las salidas de las galerías.
        float zp = Z0 + Pasarela;
        var columnas = new List<Vector3>
        {
            new Vector3(-Columna, 0, Z0), new Vector3(Columna, 0, Z0),
            new Vector3(-Columna, 0, zp), new Vector3(Columna, 0, zp),
            PL(zp + 0.4f, 0, AnchoEscalera + 0.35f), PR(-MurosMacizos, 0, 0.35f),
            PL(0, 0, AnchoEscalera + 0.35f), PR(0, 0, 0.35f),
            PL(MurosMacizos, 0, 0.35f), PR(MurosMacizos, 0, 0.35f),
            new Vector3(-Columna, 0, Z1), new Vector3(Columna, 0, Z1),
        };
        foreach (var p in columnas) PonerColumna(p, Arriba);

        // ---------- Pasarela del frente, de muro a muro ----------
        Rejilla(PL(Z0, Piso - 0.04f, 0.05f), PR(Z0, Piso - 0.04f, 0.05f), PR(zp, Piso - 0.04f, 0.05f), PL(zp, Piso - 0.04f, 0.05f));
        for (float x = -Columna; x <= Columna + 0.01f; x += Columna) // largueros debajo
            Barra("PinturaBlanca", new Vector3(x, Piso - 0.16f, Z0), new Vector3(x, Piso - 0.16f, zp), 0.08f, 0.16f);
        float anchoFrente = (XR(Z0 + Pasarela / 2) - XL(Z0 + Pasarela / 2));
        Colision("Pasarela", new Vector3(0, Piso - 0.05f, Z0 + Pasarela / 2), new Vector3(anchoFrente, 0.1f, Pasarela), Quaternion.identity);
        Baranda(PL(Z0, Piso, 0.08f), PR(Z0, Piso, 0.08f));
        // Atrás, la baranda deja libre la llegada de la escalera y la pasarela lateral.
        Baranda(PL(zp, Piso, AnchoEscalera + 0.2f), PR(zp, Piso, Lateral + 0.05f));

        // ---------- Pasarela pegada al módulo de la derecha ----------
        Rejilla(PR(zp, Piso - 0.04f, Lateral), PR(zp, Piso - 0.04f, 0.05f), PR(FinLateral, Piso - 0.04f, 0.05f), PR(FinLateral, Piso - 0.04f, Lateral));
        {
            var a = PR(zp, Piso, Lateral / 2); var b = PR(FinLateral, Piso, Lateral / 2);
            var giro = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            Colision("PasarelaLateral", (a + b) / 2 - Vector3.up * 0.05f, new Vector3(Lateral, 0.1f, (b - a).magnitude), giro);
        }
        Baranda(PR(zp, Piso, Lateral), PR(FinLateral, Piso, Lateral));
        Baranda(PR(FinLateral, Piso, Lateral), PR(FinLateral, Piso, 0.05f));
        foreach (float z in new[] { 0.8f, FinLateral })
        {
            Cano("PinturaBlanca", PR(z, 0, Lateral), PR(z, Piso, Lateral), 0.05f);
            ColisionEntre("Parante", PR(z, 0, Lateral), PR(z, Piso, Lateral), 0.12f, 0.12f);
        }

        // ---------- Escalera pegada al módulo de la izquierda: baja desde la pasarela hacia el fondo ----------
        var dirMuro = (PL(1, 0, 0) - PL(0, 0, 0)).normalized;      // a lo largo del muro, hacia el fondo
        var haciaAdentro = Vector3.Cross(Vector3.up, dirMuro).normalized;
        if (haciaAdentro.x < 0) haciaAdentro = -haciaAdentro;
        var arranqueMuro = PL(zp, 0, 0) + haciaAdentro * 0.1f;          // arriba, del lado del muro
        float contrahuella = Piso / Escalones;
        Vector3 Punto(float avance, float adentro, float y) => arranqueMuro + dirMuro * avance + haciaAdentro * adentro + Vector3.up * y;
        float largoEscalera = Escalones * Huella; // por las narices de los escalones, de la pasarela al piso
        for (int i = 1; i < Escalones; i++) // el escalón de más arriba es la pasarela
        {
            float y = Piso - i * contrahuella;
            float a = (i - 1) * Huella, b = i * Huella;
            Caja("Galvanizado",
                new[] { Punto(a, 0.04f, y - 0.04f), Punto(a, AnchoEscalera - 0.04f, y - 0.04f), Punto(b, AnchoEscalera - 0.04f, y - 0.04f), Punto(b, 0.04f, y - 0.04f) },
                new[] { Punto(a, 0.04f, y), Punto(a, AnchoEscalera - 0.04f, y), Punto(b, AnchoEscalera - 0.04f, y), Punto(b, 0.04f, y) });
        }
        // Zancas (las dos vigas inclinadas) y barandas de los dos lados.
        foreach (float lado in new[] { 0f, AnchoEscalera })
        {
            Barra("PinturaBlanca", Punto(0, lado, Piso - 0.1f), Punto(largoEscalera, lado, -0.1f), 0.06f, 0.22f);
            Baranda(Punto(0, lado, Piso), Punto(largoEscalera, lado, 0), false);
        }
        Cano("PinturaBlanca", Punto(largoEscalera * 0.55f, AnchoEscalera, 0), Punto(largoEscalera * 0.55f, AnchoEscalera, Piso * 0.45f - 0.1f), 0.05f);
        {
            // Rampa invisible por encima de las narices de los escalones: se sube caminando.
            var arribaRampa = Punto(0, AnchoEscalera / 2, Piso - 0.02f);
            var abajo = Punto(largoEscalera, AnchoEscalera / 2, 0.0f);
            var dir = (abajo - arribaRampa).normalized;
            var giro = Quaternion.LookRotation(dir, Vector3.Cross(dir, haciaAdentro).y > 0 ? Vector3.Cross(dir, haciaAdentro) : -Vector3.Cross(dir, haciaAdentro));
            var normal = giro * Vector3.up;
            Colision("Escalera", (arribaRampa + abajo) / 2 - normal * 0.05f, new Vector3(AnchoEscalera, 0.1f, Vector3.Distance(arribaRampa, abajo)), giro);
            ColisionEntre("BarandaEscalera", Punto(0, AnchoEscalera, Piso + 0.55f), Punto(largoEscalera, AnchoEscalera, 0.55f), 0.08f, 1.1f);
        }

        // ---------- Marco de arriba ----------
        var vigas = new List<(Vector3, Vector3)>
        {
            (PL(Z0, Arriba, 0.05f), PR(Z0, Arriba, 0.05f)),
            (PL(Z1, Arriba, 0.05f), PR(Z1, Arriba, 0.05f)),
            (PL(Z0, Arriba, 0.35f), PL(Z1, Arriba, 0.35f)),
            (PR(Z0, Arriba, 0.35f), PR(Z1, Arriba, 0.35f)),
            (new Vector3(-Columna, Arriba, Z0), new Vector3(-Columna, Arriba, Z1)),
            (new Vector3(Columna, Arriba, Z0), new Vector3(Columna, Arriba, Z1)),
        };
        for (float z = Z0 + 1.6f; z < Z1 - 0.5f; z += 1.6f) vigas.Add((PL(z, Arriba, 0.05f), PR(z, Arriba, 0.05f)));
        foreach (var (a, b) in vigas) Barra("PinturaBlanca", a, b, 0.1f, 0.16f);
        // Malla a los costados, con los equipos de aire encima; el medio queda abierto sobre la bóveda.
        float ym = Arriba + 0.085f;
        Get("PortonMalla").DobleCara(PL(Z0, ym, 0.05f), new Vector3(-Columna, ym, Z0), new Vector3(-Columna, ym, Z1), PL(Z1, ym, 0.05f), true);
        Get("PortonMalla").DobleCara(new Vector3(Columna, ym, Z0), PR(Z0, ym, 0.05f), PR(Z1, ym, 0.05f), new Vector3(Columna, ym, Z1), true);
        foreach (var (lado, z) in new[] { (-1, -2.2f), (-1, -0.6f), (1, 1.6f) })
        {
            float x = lado < 0 ? (XL(z) - Columna) / 2 : (XR(z) + Columna) / 2;
            var p = new Vector3(x, ym, z);
            Caja("ChapaOscura",
                new[] { p + new Vector3(-0.42f, 0, -0.42f), p + new Vector3(0.42f, 0, -0.42f), p + new Vector3(0.42f, 0, 0.42f), p + new Vector3(-0.42f, 0, 0.42f) },
                new[] { p + new Vector3(-0.42f, 0.7f, -0.42f), p + new Vector3(0.42f, 0.7f, -0.42f), p + new Vector3(0.42f, 0.7f, 0.42f), p + new Vector3(-0.42f, 0.7f, 0.42f) });
            Get("MetalNegro").Quad(p + new Vector3(-0.3f, 0.705f, -0.3f), p + new Vector3(0.3f, 0.705f, -0.3f), p + new Vector3(0.3f, 0.705f, 0.3f), p + new Vector3(-0.3f, 0.705f, 0.3f), Vector3.up);
        }
        // Baranda oxidada del borde de arriba, adelante y atrás.
        foreach (float z in new[] { Z0, Z1 })
        {
            var a = PL(z, Arriba + 0.08f, 0.1f); var b = PR(z, Arriba + 0.08f, 0.1f);
            Cano("ChapaOxido", a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, 0.03f, 6);
            Cano("PinturaBlanca", a + Vector3.up * 0.5f, b + Vector3.up * 0.5f, 0.02f, 6);
            int n = Mathf.CeilToInt(Vector3.Distance(a, b) / 1.6f);
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); Cano("PinturaBlanca", p, p + Vector3.up, 0.022f, 6); }
        }

        // ---------- Bóveda de policarbonato entre los dos muros ----------
        const int Gajos = 10;
        float Arco(float t) => 6.5f + 0.6f * (1 - t * t); // t de -1 (muro izquierdo) a 1 (derecho)
        Vector3 Boveda(float t, float z)
        {
            float xl = XL(z) + 0.05f, xr = XR(z) - 0.05f;
            return new Vector3(Mathf.Lerp(xl, xr, (t + 1) / 2), Arco(t), z);
        }
        var cortes = new List<float>();
        for (float z = Z0; z < Z1 - 0.01f; z += 1.6f) cortes.Add(z);
        cortes.Add(Z1);
        var poli = Get("Policarbonato");
        for (int k = 0; k + 1 < cortes.Count; k++)
            for (int i = 0; i < Gajos; i++)
            {
                float t0 = -1 + 2f * i / Gajos, t1 = -1 + 2f * (i + 1) / Gajos;
                poli.DobleCara(Boveda(t0, cortes[k]), Boveda(t1, cortes[k]), Boveda(t1, cortes[k + 1]), Boveda(t0, cortes[k + 1]));
            }
        foreach (float z in cortes) // arcos
            for (int i = 0; i < Gajos; i++)
                Barra("PinturaBlanca", Boveda(-1 + 2f * i / Gajos, z) - Vector3.up * 0.03f, Boveda(-1 + 2f * (i + 1) / Gajos, z) - Vector3.up * 0.03f, 0.05f, 0.06f);
        for (int i = 1; i < Gajos; i++) // nervios del policarbonato a lo largo
            for (int k = 0; k + 1 < cortes.Count; k++)
                Barra("PinturaBlanca", Boveda(-1 + 2f * i / Gajos, cortes[k]) - Vector3.up * 0.02f, Boveda(-1 + 2f * i / Gajos, cortes[k + 1]) - Vector3.up * 0.02f, 0.03f, 0.03f);
        // Soportes de la bóveda contra los muros.
        foreach (float z in cortes)
        {
            Barra("PinturaBlanca", PL(z, Arco(-1) - 0.05f, 0.02f), PL(z, Arriba, 0.35f), 0.05f, 0.05f);
            Barra("PinturaBlanca", PR(z, Arco(1) - 0.05f, 0.02f), PR(z, Arriba, 0.35f), 0.05f, 0.05f);
        }

        // ---------- Lámpara colgante debajo de la pasarela, con la luz que ya tenía el conector ----------
        var lampara = new Vector3(0, Piso - 0.55f, Z0 + Pasarela / 2);
        Cano("Galvanizado", lampara + Vector3.up * 0.22f, new Vector3(lampara.x, Piso - 0.2f, lampara.z), 0.012f, 4);
        {
            var g = Get("Galvanizado");
            for (int i = 0; i < 10; i++)
            {
                float a0 = i * Mathf.PI / 5, a1 = (i + 1) * Mathf.PI / 5;
                Vector3 Anillo(float a, float r, float y) => lampara + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                g.Quad(Anillo(a0, 0.07f, 0.22f), Anillo(a1, 0.07f, 0.22f), Anillo(a1, 0.3f, -0.05f), Anillo(a0, 0.3f, -0.05f), new Vector3(Mathf.Cos(a0 + 0.3f), 0.6f, Mathf.Sin(a0 + 0.3f)));
                g.Quad(Anillo(a0, 0.07f, 0.22f), Anillo(a1, 0.07f, 0.22f), Anillo(a1, 0.3f, -0.05f), Anillo(a0, 0.3f, -0.05f), -new Vector3(Mathf.Cos(a0 + 0.3f), 0.6f, Mathf.Sin(a0 + 0.3f)));
            }
            var foco = Get("FarolLuz");
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 4, a1 = (i + 1) * Mathf.PI / 4;
                var cc = lampara + Vector3.up * 0.02f;
                foco.Quad(cc, cc + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.12f, cc + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.12f, cc, Vector3.down);
            }
        }
        var luz = c.Find("Luz");
        if (luz != null)
        {
            Undo.RecordObject(luz, "Mover luz del conector");
            luz.position = marco.TransformPoint(lampara - Vector3.up * 0.05f);
        }

        // ---------- Detalles de los muros: puertas, aires, gas, matafuegos ----------
        var nL = Vector3.Cross(dirMuro, Vector3.up).normalized; if (nL.x < 0) nL = -nL;   // normal del muro izquierdo
        var dirR = (PR(1, 0, 0) - PR(0, 0, 0)).normalized;
        var nR = Vector3.Cross(dirR, Vector3.up).normalized; if (nR.x > 0) nR = -nR;       // normal del muro derecho
        void Puerta(Vector3 pie, Vector3 normal, Vector3 aLoLargo)
        {
            var l = aLoLargo.normalized * 0.45f; var f = normal * 0.05f; var h = Vector3.up * 2.05f;
            Caja("PinturaBlanca", new[] { pie - l, pie + l, pie + l + f, pie - l + f }, new[] { pie - l + h, pie + l + h, pie + l + h + f, pie - l + h + f });
            Caja("MetalNegro", new[] { pie + l * 0.75f + Vector3.up * 1.0f + f, pie + l * 0.6f + Vector3.up * 1.0f + f, pie + l * 0.6f + Vector3.up * 1.0f + f * 1.6f, pie + l * 0.75f + Vector3.up * 1.0f + f * 1.6f },
                               new[] { pie + l * 0.75f + Vector3.up * 1.05f + f, pie + l * 0.6f + Vector3.up * 1.05f + f, pie + l * 0.6f + Vector3.up * 1.05f + f * 1.6f, pie + l * 0.75f + Vector3.up * 1.05f + f * 1.6f });
        }
        Puerta(PL(Z0 + 0.9f, Piso, 0), nL, dirMuro);
        Puerta(PR(FinLateral - 0.8f, Piso, 0), nR, dirR);
        Aire(PR(-1.2f, 5.0f, 0), nR, dirR);
        Aire(PR(-1.2f, 5.75f, 0), nR, dirR);
        Aire(PR(0.6f, 5.4f, 0), nR, dirR);
        Aire(PR(1.0f, 2.55f, 0), nR, dirR);
        Aire(PL(2.4f, 5.2f, 0), nL, dirMuro);
        // Caño de gas amarillo y matafuegos rojo (de pared) en el módulo de la derecha.
        Cano("CordonAmarillo", PR(2.2f, 0, 0.08f), PR(2.2f, 2.9f, 0.08f), 0.03f, 6);
        Cano("CordonAmarillo", PR(2.2f, 2.9f, 0.08f), PR(1.7f, 2.9f, 0.08f), 0.03f, 6);
        foreach (var (p, n, d) in new[] { (PR(-2.2f, 1.15f, 0), nR, dirR), (PL(2.6f, 1.15f, 0), nL, dirMuro) })
        {
            var l = d * 0.27f; var f = n * 0.2f; var h = Vector3.up * 0.35f;
            Caja("CartelRojo", new[] { p - l - h, p + l - h, p + l - h + f, p - l - h + f }, new[] { p - l + h, p + l + h, p + l + h + f, p - l + h + f });
        }
        // Rejilla de ventilación galvanizada al pie del muro de la derecha.
        {
            var p = PR(3.0f, 0.5f, 0); var l = dirR * 0.3f; var f = nR * 0.06f; var h = Vector3.up * 0.45f;
            Caja("Galvanizado", new[] { p - l - h, p + l - h, p + l - h + f, p - l - h + f }, new[] { p - l + h, p + l + h, p + l + h + f, p - l + h + f });
            for (int i = 0; i < 8; i++)
                Barra("ChapaOscura", p - l + f + Vector3.up * (-0.38f + i * 0.11f), p + l + f + Vector3.up * (-0.38f + i * 0.11f), 0.03f, 0.02f);
        }

        // ---------- Mallas por material ----------
        var contenedor = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        foreach (var kv in batches)
        {
            var b = kv.Value;
            if (b.v.Count == 0) continue;
            var mesh = new Mesh { name = c.name + "_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(b.v); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key);
            hijo.transform.SetParent(est.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = hijo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = b.mat;
            if (kv.Key == "Policarbonato" || kv.Key == "Ripio") mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var t in est.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
        batches.Clear();
    }

    // El pasto del terreno no tiene que atravesar el ripio.
    static void SacarPasto(Transform c)
    {
        var terreno = Terrain.activeTerrain;
        if (terreno == null) return;
        var td = terreno.terrainData;
        int res = td.detailResolution;
        var origen = terreno.transform.position;
        var esquinas = new[] { PL(Z0 - 0.6f, 0, -0.3f), PR(Z0 - 0.6f, 0, -0.3f), PR(Z1 + 0.6f, 0, -0.3f), PL(Z1 + 0.6f, 0, -0.3f) }.Select(p => marco.TransformPoint(p)).ToArray();
        float minX = esquinas.Min(p => p.x), maxX = esquinas.Max(p => p.x), minZ = esquinas.Min(p => p.z), maxZ = esquinas.Max(p => p.z);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - origen.x) / td.size.x * res), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origen.x) / td.size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origen.z) / td.size.z * res), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origen.z) / td.size.z * res), 0, res - 1);
        Undo.RegisterCompleteObjectUndo(td, "Sacar pasto debajo de los conectores");
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, capa);
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    var p = new Vector3(origen.x + (x0 + i + 0.5f) / res * td.size.x, 0, origen.z + (z0 + j + 0.5f) / res * td.size.z);
                    var local = marco.InverseTransformPoint(p);
                    if (local.z < Z0 - 0.6f || local.z > Z1 + 0.6f) continue;
                    if (local.x < XL(local.z) - 0.3f || local.x > XR(local.z) + 0.3f) continue;
                    datos[j, i] = 0;
                }
            td.SetDetailLayer(x0, z0, capa, datos);
        }
        EditorUtility.SetDirty(td);
    }

    [MenuItem("Riftwalker/Universidad/Rehacer conexiones entre módulos")]
    public static void Aplicar()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        var raiz = GameObject.Find(Raiz);
        if (raiz == null) throw new InvalidOperationException("No se encontró " + Raiz + ".");
        foreach (var m in new[] { "PinturaBlanca", "ChapaOxido", "PortonMalla", "Galvanizado", "Vereda", "ChapaOscura", "MetalNegro", "PosteBlanco", "CordonAmarillo", "CartelRojo", "FarolLuz" }) Get(m);
        MaterialesNuevos();
        batches.Clear();

        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Rehacer conexiones entre módulos");
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(new Mesh { name = "Conectores" }, MeshPath);

        foreach (var nombre in Conectores)
        {
            var c = raiz.transform.Find(nombre);
            if (c == null) throw new InvalidOperationException("No se encontró " + nombre + ".");
            foreach (var viejo in Viejos.Concat(new[] { "Estructura" }))
            {
                var t = c.Find(viejo);
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            }
        }
        Physics.SyncTransforms();
        foreach (var nombre in Conectores)
        {
            var c = raiz.transform.Find(nombre);
            Armar(c);
            SacarPasto(c);
        }
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupo);
        Debug.Log("Conexiones entre módulos rehechas. Falta revisarlas y guardar la escena.");
    }

    // Policarbonato traslúcido y ripio: se crean una sola vez.
    static void MaterialesNuevos()
    {
        const string poli = "Assets/Materials/Uni/Uni_Policarbonato.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(poli) == null)
        {
            AssetDatabase.CopyAsset("Assets/Materials/Uni/Uni_VidrioAtrio7b.mat", poli);
            var m = AssetDatabase.LoadAssetAtPath<Material>(poli);
            m.SetColor("_BaseColor", new Color(0.9f, 0.92f, 0.93f, 0.38f));
            m.SetFloat("_Smoothness", 0.55f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }
        const string textura = "Assets/Materials/Uni/Uni_Ripio.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(textura) == null)
        {
            // Piedritas grises de distintos tonos sobre tierra oscura; se repite sin costura.
            const int n = 512;
            var rnd = new System.Random(7);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) { float g = 0.30f + (float)rnd.NextDouble() * 0.05f; px[i] = new Color(g, g * 0.97f, g * 0.93f); }
            for (int s = 0; s < 9000; s++)
            {
                int cx = rnd.Next(n), cy = rnd.Next(n); float r = 1.5f + (float)rnd.NextDouble() * 3.2f;
                float g = 0.36f + (float)rnd.NextDouble() * 0.32f; float tinte = (float)rnd.NextDouble() * 0.04f;
                for (int dy = -5; dy <= 5; dy++)
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        if (d > 1) continue;
                        float luz = g * (1.08f - 0.25f * d) + (dy < 0 ? -0.03f : 0.02f);
                        px[((cy + dy + n) % n) * n + (cx + dx + n) % n] = new Color(luz + tinte, luz, luz - tinte);
                    }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(textura, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(textura);
        }
        const string ripio = "Assets/Materials/Uni/Uni_Ripio.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(ripio) == null)
        {
            var m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_HormigonManchado.mat"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textura));
            m.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.08f);
            AssetDatabase.CreateAsset(m, ripio);
        }
    }
}
