using System.Collections.Generic;
using UnityEngine;

// US 021, CA6 · Mientras alguien usa la Vista aérea, aparecen unos ojos verdes brillantes en el cielo, arriba de su
// posición, y lo delatan. Los ven todos los jugadores (en las demás computadoras los crea JugadorEnRed). Se arman por
// código: dos ojos con la pupila en ranura que siempre miran a la cámara, que se abren al aparecer y se cierran al irse.
public class OjosEnElCielo : MonoBehaviour
{
    private const float Altura = 16f;      // m sobre el jugador
    private const float Separacion = 1.7f; // m entre el centro y cada ojo
    private const float Abrir = 0.35f;
    private static readonly Color Verde = new Color(0.3f, 1f, 0.35f);

    private static readonly Dictionary<Transform, OjosEnElCielo> activos = new Dictionary<Transform, OjosEnElCielo>();
    private static Material materialOjo, materialPupila;
    private static Mesh esfera;

    private Transform jugador;
    private readonly List<Transform> ojos = new List<Transform>();
    private float apertura, objetivo = 1f;

    /// <summary>Muestra (o saca) los ojos arriba de ese jugador.</summary>
    public static void Mostrar(Transform jugador, bool mostrar)
    {
        if (jugador == null) return;
        activos.TryGetValue(jugador, out OjosEnElCielo ojos);
        if (mostrar)
        {
            if (ojos == null)
            {
                ojos = new GameObject("Ojos en el cielo").AddComponent<OjosEnElCielo>();
                ojos.Armar(jugador);
                activos[jugador] = ojos;
            }
            ojos.objetivo = 1f;
        }
        else if (ojos != null) ojos.objetivo = 0f;
    }

    private void Armar(Transform jugador)
    {
        this.jugador = jugador;
        gameObject.layer = 2; // Ignore Raycast
        for (int lado = -1; lado <= 1; lado += 2)
        {
            Transform ojo = Pieza("Ojo", transform, MaterialOjo(), new Vector3(lado * Separacion, 0f, 0f), new Vector3(2.4f, 1.1f, 0.3f));
            // La pupila, una ranura negra un poco hacia la cámara (el frente es -z: el ojo mira a quien lo mira).
            Pieza("Pupila", ojo, MaterialPupila(), new Vector3(0f, 0f, -0.55f), new Vector3(0.18f, 0.85f, 0.4f));
            ojos.Add(ojo);
        }
        Seguir();
    }

    private static Transform Pieza(string nombre, Transform padre, Material material, Vector3 lugar, Vector3 escala)
    {
        var go = new GameObject(nombre);
        go.layer = 2;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = Esfera();
        MeshRenderer malla = go.AddComponent<MeshRenderer>();
        malla.sharedMaterial = material;
        malla.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        malla.receiveShadows = false;
        return go.transform;
    }

    private void LateUpdate()
    {
        if (jugador == null) objetivo = 0f;
        apertura = Mathf.MoveTowards(apertura, objetivo, Time.deltaTime / Abrir);
        if (apertura <= 0f && objetivo <= 0f)
        {
            if (jugador != null && activos.TryGetValue(jugador, out OjosEnElCielo yo) && yo == this) activos.Remove(jugador);
            Destroy(gameObject);
            return;
        }
        Seguir();
        // Se abren (y parpadean de vez en cuando) achicando el alto.
        float parpadeo = Mathf.Repeat(Time.time, 2.8f) < 0.12f ? 0.15f : 1f;
        float alto = Mathf.SmoothStep(0f, 1f, apertura) * parpadeo;
        foreach (Transform ojo in ojos) ojo.localScale = new Vector3(2.4f, 1.1f * Mathf.Max(0.02f, alto), 0.3f);
    }

    // Arriba del jugador, siempre de frente a la cámara de esta computadora.
    private void Seguir()
    {
        if (jugador != null) transform.position = jugador.position + Vector3.up * Altura;
        Camera camara = Camera.main;
        if (camara != null)
        {
            Vector3 mirada = transform.position - camara.transform.position;
            if (mirada.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(mirada, Vector3.up);
        }
    }

    private static Mesh Esfera()
    {
        if (esfera == null)
        {
            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera = temporal.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporal);
        }
        return esfera;
    }

    // Sin iluminación y con un verde más fuerte que 1: con el Bloom brilla en el cielo.
    private static Material MaterialOjo() => materialOjo != null ? materialOjo : materialOjo = Plano("Ojos (verde)", Verde * 3f);
    private static Material MaterialPupila() => materialPupila != null ? materialPupila : materialPupila = Plano("Ojos (pupila)", Color.black);

    private static Material Plano(string nombre, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = nombre, color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        return m;
    }
}
