using System.Collections.Generic;
using UnityEngine;

// US 313, Campo minado (carta de Invasión): una granada del Gran Capitán que tocó el escenario (el piso o una pared)
// queda clavada ahí como mina, con la punta en el lugar del choque y en la posición en que venía, con la cola roja de la
// cápsula titilando rápido (CapsulaDeGranada), y explota (con su daño de siempre, Grenade1) cuando un invasor pasa a menos de 2 m medidos
// en horizontal (así también la hace explotar un Watcher que vuela por encima). Hay un tope de minas a la vez (5, 7 y
// 9 con la carta repetida): al poner una de más, la más vieja explota sola.
public class MinaDeGranada : MonoBehaviour
{
    public const float Alcance = 2f;          // m, en horizontal
    private const float AltoMaximo = 12f;      // m de diferencia de altura (los que vuelan alto no)
    private const float Armada = 0.4f;         // s hasta que se arma (que no explote con el que la pisó al caer)

    /// <summary>Cuántas minas puede haber a la vez (lo pone la carta).</summary>
    public static int Tope = 5;

    private static readonly List<MinaDeGranada> minas = new List<MinaDeGranada>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => minas.Clear();

    private Grenade1 granada;
    private float desde;

    /// <summary>Deja la granada quieta en ese punto del escenario, como mina.</summary>
    public static void Poner(Grenade1 granada, Vector3 punto)
    {
        Rigidbody body = granada.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        granada.esMina = true;
        foreach (Collider c in granada.GetComponentsInChildren<Collider>()) c.isTrigger = true; // no se choca con nada más

        var mina = granada.gameObject.AddComponent<MinaDeGranada>();
        mina.granada = granada;
        mina.desde = Time.time;
        // La luz de la bronca (US 330), si salió con bronca, no queda en la mina: la mina se nota por la cola que titila.
        Transform bronca = granada.transform.Find("Bronca");
        if (bronca != null) Destroy(bronca.gameObject);
        CapsulaDeGranada capsula = granada.GetComponent<CapsulaDeGranada>();
        if (capsula != null) capsula.Clavar(punto);
        else granada.transform.position = punto;

        minas.Add(mina);
        // El tope: la más vieja explota sola.
        while (minas.Count > Mathf.Max(1, Tope))
        {
            MinaDeGranada vieja = minas[0];
            minas.RemoveAt(0);
            if (vieja != null && vieja.granada != null) vieja.granada.ExplotarAhora();
        }
    }

    private void OnDestroy() => minas.Remove(this);

    private void Update()
    {
        if (granada == null) return;
        if (Time.time - desde < Armada) return;

        Vector3 p = transform.position;
        foreach (Npc npc in Npc.Todos)
        {
            if (npc == null || !npc.Vivo) continue;
            Vector3 d = npc.transform.position - p;
            if (Mathf.Abs(d.y) > AltoMaximo) continue;
            d.y = 0f;
            if (d.sqrMagnitude > Alcance * Alcance) continue;
            granada.ExplotarAhora();
            return;
        }
    }
}
