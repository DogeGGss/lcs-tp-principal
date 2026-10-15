using System.Collections.Generic;
using TMPro;
using UnityEngine;

// US 120 · Panel del campo de práctica, al lado de la línea de tiro (lo pone el menú Riftwalker > Mapa de práctica).
// - Pone un bot (BotDePractica, el muñeco de Scenes/Pruebas_Armas) en cada lugar de "lugares": los de la zona de blancos.
// - Botones (mirarlos de cerca y apretar E, la tecla de Plantar, o pegarles un tiro):
//   * "Mover bots" / "Detener bots": los bots del piso caminan de costado o se quedan quietos.
//   * "Reiniciar" (CA2): levanta a todos los bots en su lugar, borra las marcas de bala (CA4) y pone el marcador en 0.
// - Marcador: cuántos bots bajó el jugador y cuántos con un tiro a la cabeza.
public class PanelDePractica : MonoBehaviour
{
    [Tooltip("El muñeco que hace de bot (Prefabs/MuniecoPrueba).")]
    public GameObject munieco;
    [Tooltip("Dónde va cada bot (sus pies). Se pueden mover en la escena. Los que no están en el piso no caminan.")]
    public Transform[] lugares = { };
    [Tooltip("Entre qué x caminan los bots (los bordes de la zona de blancos).")]
    public float x0 = 2.1f, x1 = 10.9f;
    public TextMeshPro marcador, ayuda;
    [Tooltip("A cuántos metros se puede usar un botón.")]
    public float alcance = 4f;

    private static readonly Color Amarillo = new Color(1f, 0.82f, 0.1f);

    private static readonly RaycastHit[] golpes = new RaycastHit[16];
    private static readonly IComparer<RaycastHit> porDistancia =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    private readonly List<BotDePractica> bots = new List<BotDePractica>();
    private Camera camara;
    private BotonDePractica[] botones;
    private BotonDePractica mirado;
    private bool moverse;
    private int bajas, cabezas;

    private void Awake() => botones = GetComponentsInChildren<BotonDePractica>(true);

    private void Start()
    {
        if (munieco != null)
        {
            Transform grupo = new GameObject("Bots de práctica").transform;
            foreach (Transform lugar in lugares)
            {
                if (lugar == null) continue;
                bool enElPiso = lugar.position.y < 0.05f;
                BotDePractica bot = BotDePractica.Crear(munieco, grupo, lugar.position, enElPiso, x0, x1);
                bot.Murio += AlMorirBot;
                bots.Add(bot);
            }
        }
        else Debug.LogWarning("PanelDePractica: falta el muñeco (Prefabs/MuniecoPrueba).");
        Mostrar();
    }

    private void Update()
    {
        Mirar();
        if (mirado != null && KeyBindings.Down(GameAction.Plantar)) mirado.Presionar();
    }

    // La cámara del jugador (la misma con la que apunta el arma).
    private Camera Camara()
    {
        if (camara != null && camara.isActiveAndEnabled) return camara;
        PlayerMovement jugador = FindAnyObjectByType<PlayerMovement>();
        camara = jugador != null ? jugador.GetComponentInChildren<Camera>() : null;
        if (camara == null) camara = Camera.main;
        return camara;
    }

    // El botón que el jugador tiene en la mira, de cerca. Se saltea lo del propio jugador (su cuerpo, sus armas), como
    // los disparos (WeaponFire).
    private void Mirar()
    {
        BotonDePractica nuevo = null;
        Camera cam = Camara();
        if (cam != null && !PauseMenu.IsPaused && !ShopUI.IsOpen)
        {
            Transform jugador = cam.transform.root;
            int cantidad = Physics.RaycastNonAlloc(cam.transform.position, cam.transform.forward, golpes, alcance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(golpes, 0, cantidad, porDistancia);
            for (int i = 0; i < cantidad; i++)
            {
                if (golpes[i].collider.transform.IsChildOf(jugador)) continue;
                nuevo = golpes[i].collider.GetComponentInParent<BotonDePractica>();
                break;
            }
        }
        if (nuevo == mirado) return;
        if (mirado != null) mirado.Resaltar(false);
        mirado = nuevo;
        if (mirado != null) mirado.Resaltar(true);
        Mostrar();
    }

    public void Presionar(BotonDePractica.Accion accion)
    {
        if (accion == BotonDePractica.Accion.MoverBots)
        {
            moverse = !moverse;
            foreach (BotDePractica bot in bots) if (bot != null) bot.Moverse = moverse;
        }
        else
        {
            foreach (BotDePractica bot in bots) if (bot != null) bot.Revivir(true);
            ImpactMarks.Clear();
            bajas = cabezas = 0;
        }
        Mostrar();
    }

    private void AlMorirBot(BotDePractica bot, bool cabeza)
    {
        bajas++;
        if (cabeza) cabezas++;
        Mostrar();
    }

    private string Nombre(BotonDePractica.Accion accion) =>
        accion == BotonDePractica.Accion.Reiniciar ? "Reiniciar" : moverse ? "Detener bots" : "Mover bots";

    private void Mostrar()
    {
        if (botones != null)
            foreach (BotonDePractica boton in botones)
                if (boton != null && boton.texto != null) boton.texto.text = Nombre(boton.accion).ToUpperInvariant();
        if (marcador != null) marcador.text = $"Bajas: {bajas}     A la cabeza: {cabezas}";
        if (ayuda != null)
        {
            string tecla = $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{KeyBindings.Label(GameAction.Plantar)}</color>";
            ayuda.text = mirado != null ? $"{tecla}  {Nombre(mirado.accion)}" : $"Mirá un botón y apretá {tecla}";
        }
    }

    // En el editor se ve dónde va cada bot.
    private void OnDrawGizmos()
    {
        if (lugares == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        foreach (Transform lugar in lugares)
            if (lugar != null) Gizmos.DrawWireCube(lugar.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.4f));
    }
}
