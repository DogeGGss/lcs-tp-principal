using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Rivales vistos por el equipo (US 194, CA4), para el minimapa.
// Cada computadora mira, unas veces por segundo, qué rivales tiene a la vista su jugador (en pantalla y sin una pared
// en el medio) y lo publica como una propiedad del jugador. Con lo que ve cada compañero se arma la lista de rivales
// que aparecen en el minimapa: mientras alguien del equipo lo ve, el punto lo sigue; cuando dejan de verlo, el punto
// queda en el último lugar y se borra a los 2 s.
// Solo funciona cuando hay equipos. Lo agrega PartidaEnRed al empezar una partida online.
public class RivalesVistos : MonoBehaviour
{
    private const string Prop = "mm.v";  // propiedad del jugador: los rivales (número de actor) que está viendo
    private const float Cada = 0.2f;     // segundos entre una mirada y la siguiente
    private const float Duracion = 2f;   // CA4: cuánto queda el punto después de que dejan de verlo
    private const float DistanciaMaxima = 120f;

    private static readonly List<Vector3> lugares = new List<Vector3>();
    private static readonly int[] Nadie = new int[0];

    /// <summary>Dónde dibujar cada rival visto (su lugar actual, o el último donde lo vieron).</summary>
    public static IReadOnlyList<Vector3> Lugares => lugares;

    private class Marca
    {
        public Vector3 lugar;
        public float hasta;
    }

    private PartidaEnRed partida;
    private float proxima;
    private int[] publicado = Nadie;
    private readonly Dictionary<int, Marca> marcas = new Dictionary<int, Marca>();
    private readonly List<int> veo = new List<int>(), vencidas = new List<int>();
    private readonly HashSet<int> vistos = new HashSet<int>();

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        lugares.Clear();
    }

    private void OnDestroy() => lugares.Clear();

    private void Update()
    {
        if (partida == null || Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + Cada;

        JugadorEnRed local = partida.Local;
        if (local == null || !EquiposTacticos.HayEquipos || !PhotonNetwork.InRoom)
        {
            marcas.Clear();
            lugares.Clear();
            Publicar(Nadie);
            return;
        }

        // Lo que ve este jugador (muerto no ve a nadie).
        veo.Clear();
        Camera camara = Camera.main;
        if (local.Vivo && camara != null)
            foreach (JugadorEnRed j in partida.Jugadores)
                if (j != null && j != local && j.Vivo && !EquiposTacticos.SonAliados(local.Actor, j.Actor) && LoVeo(camara, j))
                    veo.Add(j.Actor);
        veo.Sort();
        Publicar(veo.ToArray());

        // Lo que ve el equipo: lo propio más lo que publicó cada compañero vivo.
        vistos.Clear();
        foreach (int actor in veo) vistos.Add(actor);
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal || !EquiposTacticos.SonAliados(local.Actor, p.ActorNumber)) continue;
            JugadorEnRed companero = partida.Buscar(p.ActorNumber);
            if (companero == null || !companero.Vivo) continue;
            if (p.CustomProperties.TryGetValue(Prop, out object v) && v is int[] lista)
                foreach (int actor in lista) vistos.Add(actor);
        }

        // CA4: mientras lo ven, el punto lo sigue; después queda donde estaba y vence a los 2 s.
        float ahora = Time.unscaledTime;
        foreach (int actor in vistos)
        {
            JugadorEnRed rival = partida.Buscar(actor);
            if (rival == null || !rival.Vivo || EquiposTacticos.SonAliados(local.Actor, actor)) continue;
            if (!marcas.TryGetValue(actor, out Marca marca)) marcas[actor] = marca = new Marca();
            marca.lugar = rival.transform.position;
            marca.hasta = ahora + Duracion;
        }
        vencidas.Clear();
        foreach (KeyValuePair<int, Marca> par in marcas)
        {
            JugadorEnRed rival = partida.Buscar(par.Key);
            if (ahora > par.Value.hasta || rival == null || !rival.Vivo) vencidas.Add(par.Key);
        }
        foreach (int actor in vencidas) marcas.Remove(actor);

        lugares.Clear();
        foreach (Marca marca in marcas.Values) lugares.Add(marca.lugar);
    }

    // Está en pantalla y no hay nada del mapa entre la cámara y su cabeza o su pecho.
    private static bool LoVeo(Camera camara, JugadorEnRed rival)
    {
        Vector3 ojos = rival.Ojos.position, pecho = ojos - Vector3.up * 0.5f;
        return ALaVista(camara, rival, ojos) || ALaVista(camara, rival, pecho);
    }

    private static bool ALaVista(Camera camara, JugadorEnRed rival, Vector3 punto)
    {
        Vector3 desde = camara.transform.position;
        if ((punto - desde).sqrMagnitude > DistanciaMaxima * DistanciaMaxima) return false;
        Vector3 enPantalla = camara.WorldToViewportPoint(punto);
        if (enPantalla.z <= 0f || enPantalla.x < 0f || enPantalla.x > 1f || enPantalla.y < 0f || enPantalla.y > 1f) return false;
        if (!Physics.Linecast(desde, punto, out RaycastHit tope, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        return tope.collider.transform.IsChildOf(rival.transform); // lo primero que se toca es el propio rival
    }

    // Solo se manda por la red cuando cambia.
    private void Publicar(int[] lista)
    {
        if (Iguales(lista, publicado) || PhotonNetwork.LocalPlayer == null || !PhotonNetwork.InRoom) return;
        publicado = lista;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Prop, lista } });
    }

    private static bool Iguales(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
