using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Dibuja la mira (US 171 y US 172) según una configuración de MiraConfig: en cruz (cuatro rayitas), en círculo o
// solo un punto, con o sin punto central y borde oscuro. La usan el HUD (CombatHud) y la vista previa de las opciones.
// "apertura" son los px que se separa del centro por la dispersión del arma (mira dinámica).
public class MiraDibujo
{
    private const int Tramos = 32; // tramos rectos que forman el círculo
    private static readonly Color ColorBorde = new Color(0f, 0f, 0f, 0.85f);

    public readonly RectTransform raiz;
    private readonly Img[] lineas = new Img[4];
    private readonly Img punto;
    private readonly Img[] anillo = new Img[Tramos], anilloBorde = new Img[Tramos];
    private readonly UnityEngine.UI.Outline[] bordes = new UnityEngine.UI.Outline[5];

    private bool dibujada;
    private MiraConfig.Mira ultima;
    private float ultimaApertura;

    public MiraDibujo(Transform padre)
    {
        raiz = Node("Mira", padre);
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(0.5f, 0.5f);
        raiz.anchoredPosition = Vector2.zero;
        raiz.sizeDelta = Vector2.zero;

        // El borde del círculo va en piezas aparte, debajo: si no, el borde de un tramo taparía al tramo de al lado.
        for (int i = 0; i < Tramos; i++) anilloBorde[i] = Pieza("BordeCirculo");
        for (int i = 0; i < Tramos; i++) anillo[i] = Pieza("Circulo");
        string[] nombres = { "Arriba", "Abajo", "Izquierda", "Derecha" };
        for (int i = 0; i < 4; i++)
        {
            lineas[i] = Pieza(nombres[i]);
            bordes[i] = Borde(lineas[i]);
        }
        punto = Pieza("Punto");
        bordes[4] = Borde(punto);
    }

    private Img Pieza(string nombre)
    {
        RectTransform rect = Node(nombre, raiz);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        return Image(rect, null, Color.white);
    }

    // Borde oscuro de 1 px para que se lea sobre el cielo y sobre paredes oscuras.
    private static UnityEngine.UI.Outline Borde(Img imagen)
    {
        UnityEngine.UI.Outline borde = imagen.gameObject.AddComponent<UnityEngine.UI.Outline>();
        borde.effectColor = ColorBorde;
        borde.effectDistance = new Vector2(1f, 1f);
        return borde;
    }

    public void Aplicar(MiraConfig.Mira m, float apertura = 0f)
    {
        apertura = Mathf.Max(0f, apertura);
        if (dibujada && m.IgualA(ultima) && Mathf.Abs(apertura - ultimaApertura) < 0.05f) return;
        dibujada = true;
        ultima = m;
        ultimaApertura = apertura;

        Color color = m.Color;
        bool cruz = m.tipo == MiraConfig.Cruz, circulo = m.tipo == MiraConfig.Circulo;

        // Cruz
        float lejos = m.hueco + apertura + m.largo * 0.5f;
        Linea(0, cruz, new Vector2(0f, lejos), new Vector2(m.grosor, m.largo), color);
        Linea(1, cruz, new Vector2(0f, -lejos), new Vector2(m.grosor, m.largo), color);
        Linea(2, cruz, new Vector2(-lejos, 0f), new Vector2(m.largo, m.grosor), color);
        Linea(3, cruz, new Vector2(lejos, 0f), new Vector2(m.largo, m.grosor), color);
        for (int i = 0; i < 5; i++) bordes[i].enabled = m.borde;

        // Círculo: el largo y el hueco dan el radio.
        float radio = Mathf.Max(2f, m.hueco + m.largo + apertura);
        float tramo = 2f * Mathf.PI * radio / Tramos + 0.6f;
        for (int i = 0; i < Tramos; i++)
        {
            anillo[i].gameObject.SetActive(circulo);
            anilloBorde[i].gameObject.SetActive(circulo && m.borde);
            if (!circulo) continue;
            float angulo = i * 360f / Tramos, rad = angulo * Mathf.Deg2Rad;
            Vector2 lugar = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radio;
            Quaternion giro = Quaternion.Euler(0f, 0f, angulo + 90f);
            Tramo(anillo[i], lugar, giro, new Vector2(tramo, m.grosor), color);
            Tramo(anilloBorde[i], lugar, giro, new Vector2(tramo, m.grosor + 2f), ColorBorde);
        }

        // Punto central (o la mira entera, si es de tipo Punto).
        bool soloPunto = m.tipo == MiraConfig.Punto;
        punto.gameObject.SetActive(soloPunto || m.punto);
        float lado = soloPunto ? Mathf.Max(2f, m.grosor * 2f) : m.grosor;
        punto.rectTransform.sizeDelta = new Vector2(lado, lado);
        punto.color = color;
    }

    private void Linea(int i, bool visible, Vector2 lugar, Vector2 medida, Color color)
    {
        lineas[i].gameObject.SetActive(visible);
        lineas[i].rectTransform.anchoredPosition = lugar;
        lineas[i].rectTransform.sizeDelta = medida;
        lineas[i].color = color;
    }

    private static void Tramo(Img imagen, Vector2 lugar, Quaternion giro, Vector2 medida, Color color)
    {
        RectTransform rect = imagen.rectTransform;
        rect.anchoredPosition = lugar;
        rect.localRotation = giro;
        rect.sizeDelta = medida;
        imagen.color = color;
    }
}
