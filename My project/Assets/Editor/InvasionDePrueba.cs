using UnityEditor;
using UnityEngine;

// F08 (Modo Invasión): atajos para probar las últimas oleadas sin jugar toda la partida. Con el juego andando en una
// partida de Invasión:
// - Riftwalker > Invasión > Ir a la oleada 10: saca a los enemigos que quedan y arranca la oleada 10 (la del jefe).
//   Ir a la oleada 20, 30 y 40: lo mismo con el Jefe de cada vuelta (US 302).
// - Riftwalker > Invasión > Darme el Gran Capitán: lo pone en la mano, sin esperar a que salga en la elección (US 287).
// - Riftwalker > Invasión > Darme una carta...: abre una ventana con todas las cartas; con el juego andando, cada botón
//   la suma como si la hubieras elegido en el puesto (CartasDePrueba).
// - Riftwalker > Invasión > Matar a todos los invasores: mata a los que están vivos, como si les hubiera disparado
//   (suman sus puntos). La oleada se supera cuando ya aparecieron todos, así que puede hacer falta usarlo varias veces.
// - Riftwalker > Invasión > Cargar el puesto: deja cargado el puesto de suministros que está en el mapa (US 292).
// - Riftwalker > Invasión > Contador de invasores: una ventana que cuenta sola los vivos contra el tope (US 299, CA2),
//   los portales abiertos y cuántos se mandaron a cada portal, para probar la US 351 sin contar a mano.
public static class InvasionDePrueba
{
    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10")]
    private static void IrALaDiez() => PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>().IrA(OleadasDeInvasion.Ultima);

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 20")]
    private static void IrALaVeinte() => PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>().IrA(20);

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 30")]
    private static void IrALaTreinta() => PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>().IrA(30);

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 40")]
    private static void IrALaCuarenta() => PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>().IrA(40);

    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores")]
    private static void MatarATodos()
    {
        int cuantos = 0;
        foreach (Npc npc in new System.Collections.Generic.List<Npc>(Npc.Todos))
        {
            if (npc == null || !npc.Vivo) continue;
            npc.Vida.TakeDamage(npc.Vida.currentHealth + 1);
            cuantos++;
        }
        Debug.Log($"Invasión de prueba: {cuantos} invasores muertos.");
    }

    [MenuItem("Riftwalker/Invasión/Cargar el puesto")]
    private static void CargarElPuesto()
    {
        PuestoDeSuministros puesto = PuestoDeSuministros.Actual;
        if (puesto == null || !puesto.Hay) { Debug.Log("Invasión de prueba: no hay un puesto en el mapa (cae desde la oleada 2)."); return; }
        puesto.CargarDePrueba();
        Debug.Log($"Invasión de prueba: puesto de {puesto.NombreDelLugar} cargado.");
    }

    [MenuItem("Riftwalker/Invasión/Darme el Gran Capitán")]
    private static void DarmeElCapitan()
    {
        PlayerLoadout equipo = Object.FindFirstObjectByType<PlayerLoadout>();
        ShopItem[] armas = ConfigInvasion.Actual.armasSoloInvasion;
        if (equipo == null || armas == null || armas.Length == 0 || armas[0] == null) { Debug.LogWarning("No hay jugador o no está la ficha del Gran Capitán en ConfigInvasion."); return; }
        equipo.Recibir(armas[0]);
    }

    [MenuItem("Riftwalker/Invasión/Darme una carta...")]
    private static void DarmeUnaCarta() => CartasDePrueba.Abrir();

    [MenuItem("Riftwalker/Invasión/Contador de invasores")]
    private static void Contador() => ContadorDeInvasores.Abrir();

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10", true)]
    [MenuItem("Riftwalker/Invasión/Darme el Gran Capitán", true)]
    [MenuItem("Riftwalker/Invasión/Ir a la oleada 20", true)]
    [MenuItem("Riftwalker/Invasión/Ir a la oleada 30", true)]
    [MenuItem("Riftwalker/Invasión/Ir a la oleada 40", true)]
    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores", true)]
    [MenuItem("Riftwalker/Invasión/Cargar el puesto", true)]
    private static bool SePuede() =>
        Application.isPlaying && PartidaDeInvasion.Actual != null && PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() != null;
}

// La ventana de Riftwalker > Invasión > Darme una carta...: un botón por carta (las que todavía no tienen efecto, marcadas).
public class CartasDePrueba : EditorWindow
{
    private Vector2 scroll;

    public static void Abrir() => GetWindow<CartasDePrueba>("Cartas de prueba").Show();

    private void OnGUI()
    {
        bool sePuede = Application.isPlaying && EleccionDeInvasion.Actual != null;
        if (!sePuede) EditorGUILayout.HelpBox("Arrancá una partida de Invasión para darte cartas.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(!sePuede))
            foreach (CartaDeInvasion carta in CartaDeInvasion.Todas)
            {
                int veces = sePuede ? EleccionDeInvasion.Veces(carta) : 0;
                string texto = $"{carta.nombre} · {CartaDeInvasion.NombreDe(carta.rareza)}  ({veces}/{carta.Maximo})" +
                               (carta.aplicar == null ? "  · sin efecto todavía" : "");
                if (GUILayout.Button(texto)) EleccionDeInvasion.Actual.Dar(carta);
            }
        EditorGUILayout.EndScrollView();
    }

    private void OnInspectorUpdate() => Repaint();
}

// La ventana de Riftwalker > Invasión > Contador de invasores (US 351, CA3): cuenta sola lo que hay que mirar.
// - Vivos a la vez contra el tope de la oleada (US 299, CA2: 15 en la 1 y uno más por oleada hasta 30), y lo más alto
//   que llegó en la oleada. El Jefe no cuenta en el tope.
// - Los portales abiertos (como mucho 3) con cuántos tiene cada uno en fila, y cuántos se mandaron a un portal cada vez
//   (de 3 a 5; una jauría de Rápidos va entera).
// - US 303, CA1: cuántos Jefes cayeron y, por tipo de los que están vivos, la vida y el daño contra los de la dificultad
//   sin Jefes, con el % que suman (tiene que dar 25 % y 10 % por Jefe). "Ir a la oleada 11" cuenta como un Jefe caído y
//   "Ir a la oleada 21" como dos (IrA), así no hace falta ganarle a los Jefes. Los alfas no entran en la cuenta.
public class ContadorDeInvasores : EditorWindow
{
    private int irA = 5;
    private Vector2 scroll;

    public static void Abrir() => GetWindow<ContadorDeInvasores>("Contador de invasores").Show();

    private void OnGUI()
    {
        OleadasDeInvasion oleadas = Application.isPlaying && PartidaDeInvasion.Actual != null
            ? PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() : null;
        if (oleadas == null) { EditorGUILayout.HelpBox("Arrancá una partida de Invasión para ver el contador.", MessageType.Info); return; }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUIStyle grande = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
        EditorGUILayout.LabelField($"Oleada {oleadas.Oleada}" + (oleadas.Descanso ? "  (descanso)" : ""), grande);
        EditorGUILayout.LabelField($"Invasores de la oleada: {oleadas.Total}   ·   Quedan: {oleadas.Quedan}");
        EditorGUILayout.Space();

        int vivos = oleadas.Vivos, tope = oleadas.TopeDeVivos;
        EditorGUILayout.LabelField($"Vivos a la vez: {vivos} / {tope}", grande);
        EditorGUILayout.LabelField($"Lo más alto en esta oleada: {oleadas.PicoDeVivos}" +
                                   (oleadas.PicoDeVivos >= tope ? "  (llegó al tope)" : ""));
        if (oleadas.PicoDeVivos > tope && !oleadas.DeJefe)
            EditorGUILayout.HelpBox($"Pasó el tope: llegó a {oleadas.PicoDeVivos} con tope {tope}.", MessageType.Error);
        else if (oleadas.DeJefe)
            EditorGUILayout.HelpBox("Oleada del Jefe: el Jefe no cuenta en el tope y su escolta (desde la 20) sale sin tope.", MessageType.Info);
        if (oleadas.Total < tope)
            EditorGUILayout.HelpBox($"Esta oleada trae {oleadas.Total} invasores y el tope es {tope}: nunca llega al tope. " +
                                    "Probalo desde la oleada 3.", MessageType.Warning);
        EditorGUILayout.Space();

        int esperando = 0;
        foreach (PortalDeEnemigos p in oleadas.Portales) if (p != null) esperando += p.Fila.Count;
        EditorGUILayout.LabelField($"Portales abiertos: {oleadas.Portales.Count} / 3", grande);
        int i = 1;
        foreach (PortalDeEnemigos p in oleadas.Portales)
            if (p != null) EditorGUILayout.LabelField($"   Portal {i++}: {p.Fila.Count} en fila" + (p.Listo ? "" : " (abriéndose)"));
        EditorGUILayout.LabelField($"Esperando en los portales: {esperando}   ·   Sin portal todavía: {oleadas.SinPortal}");
        string grupos = oleadas.Grupos.Count == 0 ? "-" : string.Join(", ", oleadas.Grupos);
        EditorGUILayout.LabelField("Mandados a un portal cada vez:");
        EditorGUILayout.LabelField(grupos, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        Endurecimiento(oleadas, grande);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            irA = Mathf.Max(1, EditorGUILayout.IntField("Ir a la oleada", irA));
            if (GUILayout.Button("Ir", GUILayout.Width(50))) oleadas.IrA(irA);
        }
        EditorGUILayout.EndScrollView();
    }

    // US 303, CA1: vida y daño de un invasor vivo de cada tipo (que no sea alfa) contra los de la dificultad sin Jefes.
    private static void Endurecimiento(OleadasDeInvasion oleadas, GUIStyle grande)
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        int jefes = oleadas.JefesDerrotados;
        EditorGUILayout.LabelField($"Jefes derrotados: {jefes}", grande);
        if (config != null)
            EditorGUILayout.LabelField($"Tiene que dar: +{Mathf.RoundToInt(config.vidaPorJefe * jefes * 100f)} % de vida y " +
                                       $"+{Mathf.RoundToInt(config.danioPorJefe * jefes * 100f)} % de daño (el Jefe aparte).");
        var vistos = new System.Collections.Generic.HashSet<TipoDeNpc>();
        foreach (Npc npc in Npc.Todos)
        {
            if (npc == null || !npc.Vivo || npc.Tipo == null || npc.Alfa != null || npc.Tipo.esJefe || !vistos.Add(npc.Tipo)) continue;
            TipoDeNpc t = npc.Tipo;
            int vidaBase = DificultadDeInvasion.VidaDeEnemigo(t.vida), vida = npc.Vida.maxHealth;
            int danioBase = DificultadDeInvasion.DanioDeEnemigo(t.danio), danio = npc.Danio(t.danio);
            EditorGUILayout.LabelField($"   {t.nombre}:  vida {vidaBase} → {vida} ({Porcentaje(vidaBase, vida)})   ·   " +
                                       $"daño {danioBase} → {danio} ({Porcentaje(danioBase, danio)})");
        }
        if (vistos.Count == 0) EditorGUILayout.LabelField("   (todavía no hay invasores vivos que no sean alfas)");
    }

    private static string Porcentaje(int antes, int ahora) =>
        antes <= 0 ? "-" : $"{(ahora - antes) * 100f / antes:+0;-0;0} %";

    private void OnInspectorUpdate() => Repaint();
}
