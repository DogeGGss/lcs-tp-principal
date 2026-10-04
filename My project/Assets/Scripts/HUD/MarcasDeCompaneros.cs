using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Marca de compañeros (US 193), como en Valorant: un marcador azul arriba de la cabeza de cada compañero.
// - CA1: marcador chico del color del equipo (#58A6FF) arriba de cada compañero vivo.
// - CA2: al apuntarle a un compañero, debajo del marcador aparece su nombre.
// - CA3: se dibuja en la pantalla, no en el mundo: se ve aunque haya una pared en el medio. Los rivales no tienen.
// - CA4: mismo tamaño a cualquier distancia; cerca de la mira se vuelve casi transparente para no taparla.
// - CA5: en el Táctico, el compañero que lleva el dispositivo tiene además el ícono del dispositivo: ese ícono ya lo
//   dibuja DispositivoTactico (US 130); acá la marca azul se acomoda justo debajo.
// - CA6: solo compañeros, y solo cuando hay equipos (en Deathmatch sin equipos no hay marcadores).
// - CA7: cuando un compañero muere, su marcador desaparece.
// Lo agrega PartidaEnRed al empezar una partida online.
public class MarcasDeCompaneros : MonoBehaviour
{
    private const float Altura = 0.45f;      // metros sobre los ojos del compañero
    private const float RadioMira = 60f;     // px alrededor de la mira donde el marcador se atenúa (CA4)
    private const float AlturaIconoPortador = 2.15f, DebajoDelIcono = 30f; // m sobre el piso (igual que US 130) y px
    private const float AnchoCuerpo = 0.55f; // m: qué tan cerca del cuerpo hay que apuntar para ver el nombre (CA2)

    private class Marca
    {
        public RectTransform rect;
        public CanvasGroup grupo;
        public TextMeshProUGUI nombre;
    }

    private PartidaEnRed partida;
    private RectTransform lienzo;
    private readonly Dictionary<JugadorEnRed, Marca> marcas = new Dictionary<JugadorEnRed, Marca>();
    private readonly List<JugadorEnRed> sobran = new List<JugadorEnRed>();

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;

        var go = new GameObject("MarcasDeCompaneros", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35; // debajo del HUD (40), la tienda y la pausa
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;
    }

    private void LateUpdate()
    {
        if (partida == null || lienzo == null) return;
        Camera camara = Camera.main;
        JugadorEnRed local = partida.Local;
        bool hayEquipos = EquiposTacticos.HayEquipos && local != null && camara != null; // CA6

        // Los que ya no están (se fueron de la partida).
        sobran.Clear();
        foreach (KeyValuePair<JugadorEnRed, Marca> par in marcas)
            if (par.Key == null) { if (par.Value.rect != null) Destroy(par.Value.rect.gameObject); sobran.Add(par.Key); }
        foreach (JugadorEnRed j in sobran) marcas.Remove(j);

        if (!hayEquipos)
        {
            foreach (Marca m in marcas.Values) m.rect.gameObject.SetActive(false);
            return;
        }

        int portador = MarcadorTactico.Portador;
        Vector2 medida = lienzo.rect.size;
        foreach (JugadorEnRed j in partida.Jugadores)
        {
            if (j == null || j == local) continue;
            // CA6 y CA7: solo compañeros vivos.
            bool mostrar = j.Vivo && EquiposTacticos.SonAliados(local.Actor, j.Actor);
            // CA5: el que lleva el dispositivo ya tiene su ícono arriba (lo dibuja DispositivoTactico, US 130, a 2,15 m
            // del piso). La marca azul va justo debajo de ese ícono, para que no queden encimados.
            bool lleva = portador != 0 && j.Actor == portador;
            Vector3 cabeza = lleva ? j.transform.position + Vector3.up * AlturaIconoPortador : j.Ojos.position + Vector3.up * Altura;
            Vector3 enPantalla = mostrar ? camara.WorldToViewportPoint(cabeza) : Vector3.zero;
            if (mostrar && enPantalla.z <= 0.1f) mostrar = false; // está detrás de la cámara

            if (!marcas.TryGetValue(j, out Marca marca))
            {
                if (!mostrar) continue;
                marca = Crear();
                marcas[j] = marca;
            }
            marca.rect.gameObject.SetActive(mostrar);
            if (!mostrar) continue;

            // CA3 y CA4: en la pantalla, siempre del mismo tamaño.
            Vector2 lugar = new Vector2((enPantalla.x - 0.5f) * medida.x, (enPantalla.y - 0.5f) * medida.y);
            if (lleva) lugar.y -= DebajoDelIcono;
            marca.rect.anchoredPosition = lugar;
            marca.grupo.alpha = lugar.magnitude < RadioMira ? 0.25f : 1f;

            // CA2: el nombre, cuando la mira está sobre el compañero.
            Vector3 pecho = j.Ojos.position - Vector3.up * 0.5f;
            Vector3 hacia = pecho - camara.transform.position;
            float distancia = hacia.magnitude;
            float margen = Mathf.Atan2(AnchoCuerpo, Mathf.Max(0.5f, distancia)) * Mathf.Rad2Deg + 0.6f;
            bool apuntado = Vector3.Angle(camara.transform.forward, hacia) < margen;
            if (marca.nombre.gameObject.activeSelf != apuntado) marca.nombre.gameObject.SetActive(apuntado);
            if (apuntado && marca.nombre.text != j.Nombre) marca.nombre.text = j.Nombre;
        }
    }

    // El marcador: un rombo azul con borde oscuro y, abajo, el nombre.
    private Marca Crear()
    {
        MatchHud hud = MatchHud.Instance;
        Marca marca = new Marca { rect = Node("Marca", lienzo) };
        marca.rect.anchorMin = marca.rect.anchorMax = marca.rect.pivot = new Vector2(0.5f, 0.5f);
        marca.rect.sizeDelta = Vector2.zero;
        marca.grupo = marca.rect.gameObject.AddComponent<CanvasGroup>();
        marca.grupo.blocksRaycasts = false;

        Rombo(marca.rect, 14f, new Color(0f, 0f, 0f, 0.7f));
        Rombo(marca.rect, 10f, MatchHud.TeamColor); // CA1: #58A6FF

        RectTransform nombre = Node("Nombre", marca.rect);
        nombre.anchorMin = nombre.anchorMax = new Vector2(0.5f, 0.5f);
        nombre.pivot = new Vector2(0.5f, 1f);
        nombre.anchoredPosition = new Vector2(0f, -12f);
        nombre.sizeDelta = new Vector2(300f, 22f);
        marca.nombre = Text(nombre, hud != null ? hud.LabelFont : null, 17f, MatchHud.TeamColor, TextAlignmentOptions.Top, 4f);
        marca.nombre.fontStyle = FontStyles.Bold;
        marca.nombre.outlineWidth = 0.2f;
        marca.nombre.outlineColor = new Color32(0, 0, 0, 200);
        nombre.gameObject.SetActive(false);
        return marca;
    }

    private static void Rombo(RectTransform padre, float lado, Color color)
    {
        RectTransform rombo = Node("Rombo", padre);
        rombo.anchorMin = rombo.anchorMax = rombo.pivot = new Vector2(0.5f, 0.5f);
        rombo.sizeDelta = new Vector2(lado, lado);
        rombo.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image(rombo, null, color);
    }
}
