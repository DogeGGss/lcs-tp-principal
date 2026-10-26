using UnityEngine;

// US 257: el cuchillo que ven los demás jugadores, por código, como la granada (ManosPorCodigo): las manos van con el
// IK de los brazos a puntos del ajuste (AgarresTerceraPersona, sección Cuchillo).
// - CA1: con el cuchillo en la mano (ConCuchillo), la derecha lo tiene adelante, a la altura de la cintura, y la
//   izquierda queda adelante, en guardia.
// - CA2: con cada ataque (Atacar, cuando el dueño ataca: RpcCuchillo) la mano derecha va atrás y hacia adentro y cruza
//   en un corte hacia afuera, con los tiempos de primera persona (MeleeAttack: 0,12 s de preparación, 0,12 s de corte
//   y 0,3 s de vuelta). La izquierda acompaña un poco.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona), que puede
// dejarlo quieto en un momento del ataque (Probar) para ajustar los puntos con manijas.
public class CuchilloEnTerceraPersona : ManosPorCodigo
{
    private const float Atras = MeleeAttack.Preparacion;                 // 0,12 s: atrás y hacia adentro
    private const float Corte = Atras + MeleeAttack.Corte;               // 0,24 s: termina el corte, afuera
    private const float Vuelta = MeleeAttack.Total;                      // 0,54 s: de vuelta en guardia

    /// <summary>Lo que dura todo el ataque (s).</summary>
    public const float Dura = Vuelta;

    private float desde = -10f;

    /// <summary>Tiene el cuchillo en la mano (lo pone el que maneja la copia).</summary>
    public bool ConCuchillo { get; set; }

    public static CuchilloEnTerceraPersona Crear(GameObject personaje, ArmaEnTerceraPersona arma) =>
        Crear<CuchilloEnTerceraPersona>(personaje, arma);

    private float Pasado => Probar >= 0f ? Probar * Dura : Time.time - desde;
    private bool Atacando => Pasado < Dura;

    /// <summary>Un ataque (el dueño atacó).</summary>
    public void Atacar() => desde = Time.time;

    protected override bool Prendida => ConCuchillo;

    protected override void Pose(out Vector3 der, out Vector3 izq, out Quaternion dg, out Quaternion ig)
    {
        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        float t = Pasado;
        der = a.cuchilloDerecha;
        izq = a.cuchilloIzquierda;
        dg = Quaternion.Euler(a.cuchilloGiroDerecha);
        ig = Quaternion.Euler(a.cuchilloGiroIzquierda);
        if (!Atacando) return;
        Quaternion atrasG = Quaternion.Euler(a.cuchilloGiroAtras), corteG = Quaternion.Euler(a.cuchilloGiroCorte);
        float izqMueve;
        if (t < Atras)
        {
            float k = Suave(t / Atras);
            der = Vector3.Lerp(a.cuchilloDerecha, a.cuchilloAtras, k);
            dg = Quaternion.Slerp(dg, atrasG, k);
            izqMueve = k;
        }
        else if (t < Corte)
        {
            // El corte: un arco de adentro hacia afuera, que pasa por adelante (rápido al principio).
            float k = 1f - Mathf.Pow(1f - (t - Atras) / (Corte - Atras), 2f);
            Vector3 medio = (a.cuchilloAtras + a.cuchilloCorte) * 0.5f + Vector3.forward * a.cuchilloArco;
            der = Bezier(a.cuchilloAtras, medio, a.cuchilloCorte, k);
            dg = Quaternion.Slerp(atrasG, corteG, k);
            izqMueve = 1f;
        }
        else
        {
            float k = Suave((t - Corte) / (Vuelta - Corte));
            der = Vector3.Lerp(a.cuchilloCorte, a.cuchilloDerecha, k);
            dg = Quaternion.Slerp(corteG, dg, k);
            izqMueve = 1f - k;
        }
        // La izquierda se recoge un poco hacia el pecho mientras corta.
        izq = Vector3.Lerp(a.cuchilloIzquierda, a.cuchilloIzquierda + new Vector3(0.05f, 0.04f, -0.08f), izqMueve * a.granadaIzquierdaMueve);
    }
}
