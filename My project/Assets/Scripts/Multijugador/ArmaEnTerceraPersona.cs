using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// El arma en las manos del personaje que ven los demás jugadores (tercera persona, F26; US 243: La Porteña).
// - CA1: el arma a tamaño real va frente al pecho, en el lugar de su ajuste (AgarresTerceraPersona), y las dos manos
//   la agarran con el IK de los brazos (TwoBoneIK) en los mismos agarres que en primera persona. Así no atraviesa el
//   cuerpo ni flota separada de las manos, con cualquier personaje (CA7: el IK se arma con los huesos de cada uno).
// - CA2: el arma sigue hacia dónde mira el jugador (arriba o abajo) y los brazos con ella.
// - CA3: cada disparo da un golpe hacia atrás y arriba que vuelve con un resorte (Disparo).
// - CA4: al recargar, el arma se inclina y la mano izquierda va a sacar el cargador, baja a buscar otro, lo pone y
//   vuelve a agarrar el arma (Recarga, el avance que llega por la red).
// - CA5: al cambiar de arma, la que tiene baja y se guarda y la nueva sube desde abajo (Cambiar y Poner).
// Las armas sin ajuste quedan como antes: el arma en la mano derecha y los brazos hacia los agarres de primera persona.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo para ajustarlo (EspejoTerceraPersona).
[DefaultExecutionOrder(50)] // después de que JugadorEnRed mueve el cuerpo y gira la cabeza
public class ArmaEnTerceraPersona : MonoBehaviour
{
    private const float Guardar = 0.15f;      // lo que tarda en bajar el arma al cambiar
    private const float Resorte = 170f, Amortiguacion = 0.6f;

    private Animator animador;
    private Transform ojos;
    private RigBuilder rig;
    private Rig capa;
    private TwoBoneIKConstraint ikDerecho, ikIzquierdo;
    private Transform objetivoDerecho, objetivoIzquierdo;   // los de acá
    private Transform originalDerecho, originalIzquierdo;   // los de antes (agarres de primera persona de la Línea A)

    // El arma en la mano
    private GameObject modelo;
    private Transform soporte;           // el padre del modelo: se mueve para que el modelo quede donde va
    private AgarresTerceraPersona.Agarre ajuste;
    private Vector3 derechaLocal, izquierdaLocal;
    private Quaternion derechaGiro = Quaternion.identity, izquierdaGiro = Quaternion.identity;
    private float sacarDesde = -10f, sacarDura = 0.3f;
    private System.Action pendiente;     // mostrar la nueva cuando termine de guardar la que tiene
    private float guardarDesde = -10f;
    private Vector2 golpe, golpeVel;     // x: grados hacia arriba, y: metros hacia atrás
    private float recargaVista = -1f;

    /// <summary>El avance de la recarga (0 a 1), o -1 si no está recargando. Lo pone el que maneja la copia.</summary>
    public float Recarga { get; set; } = -1f;

    /// <summary>Tiene un arma con ajuste en la mano: el arma y las manos las pone este script.</summary>
    public bool Manejando => modelo != null && ajuste != null;

    /// <summary>Lo agrega a ese personaje. "ojos": lo que gira hacia dónde mira (la cámara de la copia).</summary>
    public static ArmaEnTerceraPersona Crear(GameObject personaje, Animator animador, Transform ojos)
    {
        if (personaje == null || animador == null || ojos == null || !animador.isHuman) return null;
        ArmaEnTerceraPersona a = personaje.AddComponent<ArmaEnTerceraPersona>();
        a.animador = animador;
        a.ojos = ojos;
        a.ArmarIK();
        return a;
    }

    // ---------- IK ----------

    // Usa el IK de los brazos que ya tiene el personaje (el del Player) apuntando a objetivos propios; si no tiene, lo arma.
    private void ArmarIK()
    {
        objetivoDerecho = new GameObject("Agarre derecho (tercera persona)").transform;
        objetivoIzquierdo = new GameObject("Agarre izquierdo (tercera persona)").transform;
        objetivoDerecho.SetParent(transform, false);
        objetivoIzquierdo.SetParent(transform, false);

        Transform manoDer = animador.GetBoneTransform(HumanBodyBones.RightHand);
        Transform manoIzq = animador.GetBoneTransform(HumanBodyBones.LeftHand);
        foreach (TwoBoneIKConstraint ik in GetComponentsInChildren<TwoBoneIKConstraint>(true))
        {
            Transform punta = ik.data.tip;
            bool derecha = punta == manoDer || (punta != manoIzq && ik.name.ToLowerInvariant().Contains("right"));
            if (derecha && ikDerecho == null) ikDerecho = ik;
            else if (!derecha && ikIzquierdo == null) ikIzquierdo = ik;
        }
        rig = GetComponentInChildren<RigBuilder>(true);

        if (ikDerecho == null || ikIzquierdo == null || rig == null)
        {
            // Sin IK (el muñeco espejo): uno nuevo, con los huesos del brazo.
            if (rig == null) rig = animador.gameObject.AddComponent<RigBuilder>();
            var go = new GameObject("IK (tercera persona)");
            go.transform.SetParent(animador.transform, false);
            capa = go.AddComponent<Rig>();
            ikDerecho = Brazo(go.transform, "Derecho", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, manoDer);
            ikIzquierdo = Brazo(go.transform, "Izquierdo", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, manoIzq);
            rig.layers.Clear();
            rig.layers.Add(new RigLayer(capa));
        }
        else
        {
            originalDerecho = ikDerecho.data.target;
            originalIzquierdo = ikIzquierdo.data.target;
            capa = ikDerecho.GetComponentInParent<Rig>();
        }
        Apuntar(ikDerecho, objetivoDerecho);
        Apuntar(ikIzquierdo, objetivoIzquierdo);
        CopiarOriginales();
        rig.Build();
    }

    private TwoBoneIKConstraint Brazo(Transform padre, string nombre, HumanBodyBones hombro, HumanBodyBones codo, Transform mano)
    {
        var go = new GameObject("Brazo " + nombre);
        go.transform.SetParent(padre, false);
        TwoBoneIKConstraint ik = go.AddComponent<TwoBoneIKConstraint>();
        TwoBoneIKConstraintData d = ik.data;
        d.root = animador.GetBoneTransform(hombro);
        d.mid = animador.GetBoneTransform(codo);
        d.tip = mano;
        d.targetPositionWeight = 1f;
        d.targetRotationWeight = 1f;
        d.hintWeight = 0f;
        ik.data = d;
        ik.weight = 1f;
        return ik;
    }

    private static void Apuntar(TwoBoneIKConstraint ik, Transform objetivo)
    {
        if (ik == null) return;
        TwoBoneIKConstraintData d = ik.data;
        d.target = objetivo;
        ik.data = d;
    }

    // Sin un arma con ajuste: las manos van donde iban antes (o, en el muñeco, quedan como en la animación).
    private void CopiarOriginales()
    {
        bool hay = originalDerecho != null && originalIzquierdo != null;
        if (hay)
        {
            objetivoDerecho.SetPositionAndRotation(originalDerecho.position, originalDerecho.rotation);
            objetivoIzquierdo.SetPositionAndRotation(originalIzquierdo.position, originalIzquierdo.rotation);
        }
        if (capa != null) capa.weight = hay || Manejando ? 1f : 0f;
    }

    // ---------- El arma ----------

    /// <summary>
    /// CA5: cambia de arma. Si tiene una con ajuste, primero la baja y la guarda y después llama a "mostrarNueva" (que
    /// muestra la nueva y llama a Poner); si no, enseguida.
    /// </summary>
    public void Cambiar(System.Action mostrarNueva)
    {
        if (mostrarNueva == null) return;
        if (!Manejando) { pendiente = null; mostrarNueva(); return; }
        if (pendiente == null) guardarDesde = Time.time;
        pendiente = mostrarNueva;
    }

    /// <summary>
    /// El arma que tiene en la mano ahora (null si ninguna): su modelo a tamaño real (hijo de un soporte) y el arma de
    /// primera persona de la que salió, con sus agarres. "sacar": lo que tarda en subirla (s); 0, ya en su lugar.
    /// </summary>
    public void Poner(GameObject modeloReal, GameObject primeraPersona, float sacar)
    {
        pendiente = null;
        modelo = modeloReal;
        soporte = modelo != null ? modelo.transform.parent : null;
        ajuste = modelo != null && soporte != null ? AgarresTerceraPersona.Actual.Buscar(primeraPersona) : null;
        golpe = golpeVel = Vector2.zero;
        recargaVista = -1f;
        sacarDura = Mathf.Max(0.05f, sacar);
        sacarDesde = sacar > 0f ? Time.time : -10f;
        if (ajuste != null) Agarres(primeraPersona);
        if (!Manejando) CopiarOriginales();
    }

    // Los agarres de primera persona del arma, pasados al modelo a tamaño real (que sale del mismo arma, ubicada en el
    // espacio de su cámara y escalada: ModeloReal).
    private void Agarres(GameObject primeraPersona)
    {
        Transform piezas = modelo.transform.Find("Piezas");
        Transform marco = primeraPersona.transform.parent != null ? primeraPersona.transform.parent : primeraPersona.transform;
        Transform der = primeraPersona.transform.Find(BrazosEnCamara.AgarreDerecho);
        Transform izq = primeraPersona.transform.Find(BrazosEnCamara.AgarreIzquierdo);
        float escala = piezas != null ? piezas.localScale.x : 1f;
        Vector3 corrido = piezas != null ? piezas.localPosition : Vector3.zero;
        if (der != null)
        {
            derechaLocal = corrido + marco.InverseTransformPoint(der.position) * escala;
            derechaGiro = Quaternion.Inverse(marco.rotation) * der.rotation;
        }
        else { derechaLocal = new Vector3(0f, -0.05f, -0.05f); derechaGiro = Quaternion.identity; }
        if (izq != null)
        {
            izquierdaLocal = corrido + marco.InverseTransformPoint(izq.position) * escala;
            izquierdaGiro = Quaternion.Inverse(marco.rotation) * izq.rotation;
        }
        else { izquierdaLocal = derechaLocal + new Vector3(-0.04f, 0f, 0f); izquierdaGiro = derechaGiro; }
    }

    /// <summary>CA3: un disparo.</summary>
    public void Disparo()
    {
        if (!Manejando) return;
        golpeVel += new Vector2(ajuste.retrocesoArriba, ajuste.retrocesoAtras) * 22f;
    }

    private void OnDisable()
    {
        if (capa != null && originalDerecho == null) capa.weight = 0f;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (pendiente != null && Time.time - guardarDesde >= Guardar)
        {
            System.Action mostrar = pendiente;
            pendiente = null;
            mostrar();
        }
        if (!Manejando || ojos == null) { CopiarOriginales(); return; }
        if (capa != null) capa.weight = 1f;

        // Retroceso: un resorte que vuelve a cero.
        float w = Mathf.Sqrt(Resorte);
        golpeVel += (-Resorte * golpe - 2f * Amortiguacion * w * golpeVel) * dt;
        golpe += golpeVel * dt;

        // Recarga: llega por la red unas 10 veces por segundo; se sigue suave.
        if (Recarga < 0f) recargaVista = -1f;
        else recargaVista = recargaVista < 0f || Recarga < recargaVista - 0.3f ? Recarga
                          : Mathf.Lerp(recargaVista, Recarga, 1f - Mathf.Exp(-12f * dt));
        float r = recargaVista;
        float inclinada = r < 0f ? 0f : Suave(Mathf.InverseLerp(0f, 0.15f, r)) * (1f - Suave(Mathf.InverseLerp(0.85f, 1f, r)));

        // Sacar (sube desde abajo) y guardar (baja): 1 = abajo del todo.
        float abajo = 0f;
        if (sacarDesde > 0f) abajo = 1f - Suave(Mathf.Clamp01((Time.time - sacarDesde) / sacarDura));
        if (pendiente != null) abajo = Mathf.Max(abajo, Suave(Mathf.Clamp01((Time.time - guardarDesde) / Guardar)));

        // Dónde va el arma: respecto de los ojos, con el retroceso, la recarga y el sacar/guardar.
        Vector3 lugar = ajuste.lugar + new Vector3(-0.06f * inclinada, 0.03f * inclinada - 0.25f * abajo, -golpe.y - 0.05f * abajo);
        Vector3 giro = ajuste.giro + new Vector3(-golpe.x - 20f * inclinada + 45f * abajo, -15f * inclinada, -25f * inclinada);
        Quaternion giroArma = ojos.rotation * Quaternion.Euler(giro);
        // Gira alrededor del pecho (no de los ojos): mirando abajo, girando alrededor de los ojos se metía en el pecho.
        Vector3 pecho = ojos.position - transform.up * ajuste.alturaPecho;
        Vector3 puntoArma = pecho + ojos.rotation * lugar;
        // Y nunca más atrás que el frente del pecho.
        Vector3 enCuerpo = transform.InverseTransformPoint(puntoArma);
        float frente = transform.InverseTransformPoint(pecho).z + ajuste.minimoAdelante;
        if (enCuerpo.z < frente) { enCuerpo.z = frente; puntoArma = transform.TransformPoint(enCuerpo); }

        modelo.transform.localScale = Vector3.one * ajuste.escala;
        // El soporte (padre del modelo) se mueve para que el modelo quede justo ahí.
        soporte.rotation = giroArma * Quaternion.Inverse(modelo.transform.localRotation);
        soporte.position = puntoArma - soporte.rotation * Vector3.Scale(modelo.transform.localPosition, soporte.lossyScale);

        Transform m = modelo.transform;
        Vector3 der = m.TransformPoint(derechaLocal + ajuste.manoDerecha);
        Quaternion derGiro = m.rotation * derechaGiro * Quaternion.Euler(ajuste.giroManoDerecha);
        Vector3 izq = m.TransformPoint(izquierdaLocal + ajuste.manoIzquierda);
        Quaternion izqGiro = m.rotation * izquierdaGiro * Quaternion.Euler(ajuste.giroManoIzquierda);

        // CA4: la mano izquierda saca el cargador (por abajo de la empuñadura), baja a buscar otro, lo pone y vuelve.
        if (r >= 0f)
        {
            Vector3 cargador = der - m.up * 0.12f;
            Vector3 bolsillo = cargador + Vector3.down * 0.25f - transform.forward * 0.05f;
            float aCargador = Suave(Mathf.InverseLerp(0.1f, 0.25f, r)) * (1f - Suave(Mathf.InverseLerp(0.8f, 0.95f, r)));
            float aBolsillo = Suave(Mathf.InverseLerp(0.3f, 0.45f, r)) * (1f - Suave(Mathf.InverseLerp(0.55f, 0.7f, r)));
            izq = Vector3.Lerp(izq, Vector3.Lerp(cargador, bolsillo, aBolsillo), aCargador);
        }

        objetivoDerecho.SetPositionAndRotation(der, derGiro);
        objetivoIzquierdo.SetPositionAndRotation(izq, izqGiro);
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
