using UnityEngine;
using UnityEngine.Rendering;

public class HideOwnBody : MonoBehaviour
{
    [SerializeField] private Camera ownCamera;

    // Con multijugador, esto tiene que correr solo en el jugador local: los demas si tienen que ver este cuerpo.
    private void Start()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (ownCamera != null && r.transform.IsChildOf(ownCamera.transform)) continue;
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
