using UnityEngine;

// US 022 · Callejero: al apretar la Q, el personaje suelta un perro espectral que aparece delante suyo y sale corriendo
// hacia el rival más cercano (CA1). Todo lo del perro está en Perro. La recarga empieza al soltarlo (CA12).
// Al tener la habilidad, se empieza a armar el NavMesh del mapa (NavMeshEnVivo) para que el perro siga los pasillos.
public class HabilidadCallejero : EfectoHabilidad
{
    /// <summary>CA15: el jugador que lo soltó, su número de perro, dónde y hacia dónde mira (lo manda JugadorEnRed).</summary>
    public static event System.Action<Transform, int, Vector3, float> Soltado;

    private static int siguienteId = 1;

    private void Start()
    {
        NavMeshEnVivo.Preparar(transform.position);
    }

    public override bool Aplicar()
    {
        NavMeshEnVivo.Preparar(transform.position);

        // Delante del personaje, a 1 m; con una pared ahí, a sus pies.
        Vector3 adelante = transform.forward;
        adelante.y = 0f;
        adelante = adelante.sqrMagnitude > 1e-4f ? adelante.normalized : Vector3.forward;
        Vector3 pies = transform.position;
        CharacterController cuerpo = GetComponent<CharacterController>();
        if (cuerpo != null) pies = transform.TransformPoint(cuerpo.center) - Vector3.up * cuerpo.height * 0.5f * transform.lossyScale.y;
        Vector3 lugar = pies + adelante * 1f;
        if (Physics.Raycast(pies + Vector3.up * 0.5f, adelante, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            lugar = pies;
        // En el piso aunque lo suelte saltando o desde un balcón (antes quedaba volando).
        if (Perro.Piso(lugar, transform, out float suelo)) lugar.y = suelo;

        float yaw = Quaternion.LookRotation(adelante).eulerAngles.y;
        JugadorEnRed red = GetComponent<JugadorEnRed>();
        int id = siguienteId++;
        Perro perro = Perro.Crear(transform, red != null ? red.Actor : -1, id, lugar, yaw, true, null);
        // CA16: el sonido de soltarlo, para el que lo usa sin distancia, por el canal de efectos (antes salía sin canal y
        // no lo regulaban los volúmenes de Opciones).
        SonidoPerro.Tocar(SonidoPerro.Tipo.Soltar, lugar, Perro.Grupo(transform), true);
        Soltado?.Invoke(transform, id, lugar, yaw);
        return perro != null;
    }
}
