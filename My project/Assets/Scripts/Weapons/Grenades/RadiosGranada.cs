using System.Collections.Generic;
using UnityEngine;

// US 073 · Granada de metralla, solo para probar el CA4 (daño según la distancia) en el editor. Se prende y apaga
// desde el menú de Unity: Riftwalker > Prueba: radios de la granada. Con la prueba prendida, cada granada de
// metralla que tira el jugador de esta computadora deja en el piso, durante unos segundos, un círculo rojo de
// 1,5 m (daño completo) y uno amarillo de 5 m (hasta ahí llega el daño), y arriba de cada jugador que lastimó un
// cartel con su distancia a la explosión y el daño que recibió (también sale en la Consola). Se ve en la
// computadora del que la tiró, que es la que calcula el daño. En una build no existe.
public static class RadiosGranada
{
#if UNITY_EDITOR
    private const string Clave = "Riftwalker.PruebaRadiosGranada";
    private const string Menu = "Riftwalker/Prueba: radios de la granada";
    private const float Duracion = 10f;
    private static readonly Color ColorCompleto = new Color(1f, 0.3f, 0.3f);
    private static readonly Color ColorAlcance = new Color(1f, 0.85f, 0.2f);
    private static readonly Color ColorDano = new Color(1f, 1f, 1f);

    private static readonly List<(HealthSystem vida, float distancia, int dano)> heridos =
        new List<(HealthSystem, float, int)>();

    private static bool Activa => UnityEditor.EditorPrefs.GetBool(Clave, false);

    [UnityEditor.MenuItem(Menu)]
    private static void Cambiar()
    {
        UnityEditor.EditorPrefs.SetBool(Clave, !Activa);
        Debug.Log("Prueba: radios de la granada: " + (Activa ? "prendida" : "apagada"));
    }

    [UnityEditor.MenuItem(Menu, true)]
    private static bool Marcar()
    {
        UnityEditor.Menu.SetChecked(Menu, Activa);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Suscribir()
    {
        heridos.Clear();
        Grenade1.Damaged -= Anotar; // por si no se recarga el dominio al darle Play
        Grenade1.Damaged += Anotar;
        Grenade1.Exploded -= Dibujar;
        Grenade1.Exploded += Dibujar;
    }

    // Grenade1 avisa primero a quién lastimó y después que explotó.
    private static void Anotar(Grenade1 granada, HealthSystem vida, float distancia, int dano)
    {
        if (Activa) heridos.Add((vida, distancia, dano));
    }

    private static void Dibujar(Grenade1 granada, Vector3 centro)
    {
        if (!Activa || granada == null || granada.Type != GrenadeType.Frag) { heridos.Clear(); return; }

        GameObject grupo = MarcasDePrueba.Grupo("Radios de la granada (prueba)", Duracion);
        Vector3 piso = MarcasDePrueba.Piso(centro) + Vector3.up * 0.04f;
        MarcasDePrueba.Circulo(grupo, piso, granada.fullDamageRadius, 0.06f, ColorCompleto);
        MarcasDePrueba.Circulo(grupo, piso, granada.radius, 0.06f, ColorAlcance);
        MarcasDePrueba.Cartel(grupo, piso + new Vector3(granada.fullDamageRadius, 0.3f, 0f), $"{granada.fullDamageRadius:0.#} m", 0.25f, ColorCompleto);
        MarcasDePrueba.Cartel(grupo, piso + new Vector3(granada.radius, 0.3f, 0f), $"{granada.radius:0.#} m", 0.25f, ColorAlcance);

        if (heridos.Count == 0) Debug.Log("Granada: no lastimó a nadie");
        foreach (var (vida, distancia, dano) in heridos)
        {
            string nombre = vida != null ? vida.name : "?";
            Debug.Log($"Granada: {nombre} a {distancia:0.00} m, {dano} de daño");
            if (vida != null)
                MarcasDePrueba.Cartel(grupo, vida.transform.position + Vector3.up * 1.5f, $"{distancia:0.0} m · {dano} de daño", 0.3f, ColorDano);
        }
        heridos.Clear();
    }
#endif
}
