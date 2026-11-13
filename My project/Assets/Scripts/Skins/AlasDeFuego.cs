using UnityEngine;

// F35 (Skins de armas), US 381: las alas del fogonazo de las legendarias (EfectosDeSkin): miran a la cámara, con el "adelante" del disparo de costado en la pantalla; se abren y se
// cierran en DuracionAlas.
public class AlasDeFuego : MonoBehaviour
{
    private const float DuracionAlas = 0.14f, TamanoAlas = 0.6f; // s y m
    private static readonly int Abrir = Shader.PropertyToID("_Abrir");
    private static readonly int ColorFuego = Shader.PropertyToID("_ColorFuego"), ColorNucleo = Shader.PropertyToID("_ColorNucleo");

    private Vector3 adelante, boca;
    private float empezo;
    private MeshRenderer r;
    private MaterialPropertyBlock bloque;

    public void Iniciar(Vector3 haciaAdelante, CatalogoDeSkins.Efectos e)
    {
        adelante = haciaAdelante.sqrMagnitude > 0.0001f ? haciaAdelante.normalized : Vector3.forward;
        boca = transform.position;
        empezo = Time.time;
        r = GetComponent<MeshRenderer>();
        bloque = new MaterialPropertyBlock();
        bloque.SetColor(ColorFuego, e.fuego);
        bloque.SetColor(ColorNucleo, e.nucleo);
        Colocar();
    }

    private void LateUpdate() => Colocar();

    private void Colocar()
    {
        float t = (Time.time - empezo) / DuracionAlas;
        if (t >= 1f) { Destroy(gameObject); return; }
        Camera cam = Camera.main;
        Vector3 x = adelante;
        float tam = TamanoAlas;
        if (cam != null)
        {
            Vector3 mira = (boca - cam.transform.position).normalized; // el cuadrado mira a la cámara
            // La x del cuadrado, a lo largo del disparo (en la pantalla); si se dispara justo hacia adelante de la
            // cámara (primera persona), de costado.
            x = Vector3.ProjectOnPlane(adelante, mira);
            // En primera persona (se dispara hacia adelante de la cámara): más chicas y hacia arriba a la derecha de la boca,
            // lejos de la mira y del arma (que se dibuja encima, con otra cámara, y las taparía).
            if (x.sqrMagnitude < 0.04f) { x = cam.transform.right * 0.6f + cam.transform.up * 0.8f; tam = TamanoAlas * 0.4f; }
            x.Normalize();
            transform.rotation = Quaternion.LookRotation(mira, Vector3.Cross(mira, x)); // (así su derecha es x)
        }
        transform.localScale = Vector3.one * tam;
        transform.position = boca + x * tam * 0.5f; // el borde izquierdo del cuadrado (x = 0) en la boca
        bloque.SetFloat(Abrir, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI)); // se abren y se cierran
        r.SetPropertyBlock(bloque);
    }
}
