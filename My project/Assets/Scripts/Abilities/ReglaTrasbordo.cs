using UnityEngine;

// US 019 · Trasbordo, solo para probar el CA1 (9 m) en el editor. Se prende y apaga desde el menú de Unity:
// Riftwalker > Prueba: regla del Trasbordo. Con la regla prendida, cada salto del jugador de esta computadora deja
// en el piso, durante unos segundos, una línea de 9 m desde donde salió con una marca por metro, una cruz donde
// llegó y un cartel con los metros que recorrió (también sale en la Consola). Funciona en cualquier mapa.
// En una build no existe.
public static class ReglaTrasbordo
{
#if UNITY_EDITOR
    private const string Clave = "Riftwalker.PruebaReglaTrasbordo";
    private const string Menu = "Riftwalker/Prueba: regla del Trasbordo";
    private const float Duracion = 10f;
    private static readonly Color ColorRegla = new Color(1f, 0.85f, 0.2f);
    private static readonly Color ColorLlegada = new Color(0.25f, 1f, 0.5f);

    private static GameObject actual;

    private static bool Activa => UnityEditor.EditorPrefs.GetBool(Clave, false);

    [UnityEditor.MenuItem(Menu)]
    private static void Cambiar()
    {
        UnityEditor.EditorPrefs.SetBool(Clave, !Activa);
        Debug.Log("Prueba: regla del Trasbordo: " + (Activa ? "prendida" : "apagada"));
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
        HabilidadTrasbordo.Usado -= Dibujar; // por si no se recarga el dominio al darle Play
        HabilidadTrasbordo.Usado += Dibujar;
    }

    private static void Dibujar(Transform jugador, Vector3 origen, Vector3 destino)
    {
        if (!Activa || jugador == null) return;
        Vector3 plano = destino - origen;
        plano.y = 0f;
        float metros = plano.magnitude;
        Debug.Log($"Trasbordo: recorrió {metros:0.00} m (CA1 pide {HabilidadTrasbordo.Distancia:0} m)");
        if (metros < 0.01f) return;

        if (actual != null) Object.Destroy(actual);
        GameObject grupo = actual = MarcasDePrueba.Grupo("Regla del Trasbordo (prueba)", Duracion);

        CharacterController cuerpo = jugador.GetComponent<CharacterController>();
        float pies = cuerpo != null ? (cuerpo.center.y - cuerpo.height * 0.5f) * jugador.lossyScale.y : -1f;
        Vector3 direccion = plano / metros;
        Vector3 costado = Vector3.Cross(Vector3.up, direccion);
        Vector3 inicio = origen + Vector3.up * (pies + 0.03f);
        float largo = HabilidadTrasbordo.Distancia;

        MarcasDePrueba.Linea(grupo, inicio, inicio + direccion * largo, 0.06f, ColorRegla);
        for (int m = 0; m <= largo; m++)
        {
            float media = m == largo ? 0.6f : m % 5 == 0 ? 0.4f : 0.25f;
            Vector3 punto = inicio + direccion * m;
            MarcasDePrueba.Linea(grupo, punto - costado * media, punto + costado * media, 0.05f, ColorRegla);
            if (m > 0) MarcasDePrueba.Cartel(grupo, punto + Vector3.up * 0.3f, m + " m", 0.2f, ColorRegla);
        }

        Vector3 llegada = destino + Vector3.up * (pies + 0.05f);
        Vector3 diagonal1 = (direccion + costado) * 0.4f, diagonal2 = (direccion - costado) * 0.4f;
        MarcasDePrueba.Linea(grupo, llegada - diagonal1, llegada + diagonal1, 0.08f, ColorLlegada);
        MarcasDePrueba.Linea(grupo, llegada - diagonal2, llegada + diagonal2, 0.08f, ColorLlegada);
        MarcasDePrueba.Cartel(grupo, llegada + Vector3.up * 2.4f, $"{metros:0.00} m", 0.4f, ColorLlegada);
    }
#endif
}
