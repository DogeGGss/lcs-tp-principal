using UnityEngine;
using UnityEngine.SceneManagement;

// Mapa 1: el viento del planeta. Al cargar Mapa_deathmatch se pone solo el objeto Resources/Mapas/Viento_Mapa1, que
// trae el sonido ambiente (AmbienteDelMapa: el viento de fondo en loop y el zumbido de las máquinas del kit sci-fi) y
// este componente, que hace volar polvo y ráfagas de viento alrededor de la cámara del jugador (así se ven en todo el
// mapa sin llenar el mapa entero de partículas). Es solo ambiente: cada computadora tiene el suyo.
public class VientoDelMapa : MonoBehaviour
{
    private static readonly string[] Escenas = { "Mapa_deathmatch" };
    private const string Recurso = "Mapas/Viento_Mapa1";

    [Tooltip("Partículas que acompañan a la cámara (polvo y ráfagas).")]
    public GameObject[] particulas;
    [Tooltip("Dónde quedan respecto de la cámara.")]
    public Vector3 corrida = new Vector3(0f, 1f, 0f);
    [Tooltip("De dónde sopla el viento (grados, 0 = hacia +Z).")]
    public float direccion = 60f;

    private Transform seguidor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        AlCargar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (!escena.isLoaded || System.Array.IndexOf(Escenas, escena.name) < 0) return;
        if (FindAnyObjectByType<VientoDelMapa>() != null) return;
        GameObject prefab = Resources.Load<GameObject>(Recurso);
        if (prefab == null) return;
        GameObject viento = Instantiate(prefab);
        viento.name = "Viento del mapa";
        SceneManager.MoveGameObjectToScene(viento, escena);
    }

    private void Start()
    {
        seguidor = new GameObject("Viento (partículas)").transform;
        seguidor.SetParent(transform, false);
        seguidor.rotation = Quaternion.Euler(0f, direccion, 0f);
        if (particulas == null) return;
        foreach (GameObject p in particulas)
            if (p != null) Instantiate(p, seguidor, false);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || seguidor == null) return;
        // Solo la posición: el viento sopla siempre para el mismo lado aunque el jugador gire.
        seguidor.position = cam.transform.position + corrida;
    }
}
