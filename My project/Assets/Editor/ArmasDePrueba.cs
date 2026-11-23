using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Para probar con cualquier arma sin comprarla: Riftwalker > Armas > Darme un arma... abre una ventana con todas las
// armas del proyecto (las fichas de la tienda, también las que son solo de Invasión, como el Gran Capitán), separadas
// en principales y secundarias. Con el juego andando, cada botón la pone en su espacio, con la munición llena, como si
// la hubieras recibido en el puesto (PlayerLoadout.Recibir). Sirve en cualquier modo.
public static class DarmeUnArma
{
    [MenuItem("Riftwalker/Armas/Darme un arma...")]
    private static void Abrir() => ArmasDePrueba.Abrir();
}

public class ArmasDePrueba : EditorWindow
{
    private Vector2 scroll;
    private List<ShopItem> principales, secundarias;

    public static void Abrir() => GetWindow<ArmasDePrueba>("Armas de prueba").Show();

    private void OnEnable() => Buscar();
    private void OnProjectChange() => Buscar();

    // Todas las fichas de armas del proyecto, por nombre.
    private void Buscar()
    {
        principales = new List<ShopItem>();
        secundarias = new List<ShopItem>();
        foreach (string guid in AssetDatabase.FindAssets("t:ShopItem"))
        {
            ShopItem item = AssetDatabase.LoadAssetAtPath<ShopItem>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null) continue;
            if (item.kind == ShopItemKind.PrimaryWeapon) principales.Add(item);
            else if (item.kind == ShopItemKind.SecondaryWeapon) secundarias.Add(item);
        }
        principales.Sort((a, b) => string.Compare(Nombre(a), Nombre(b), System.StringComparison.CurrentCulture));
        secundarias.Sort((a, b) => string.Compare(Nombre(a), Nombre(b), System.StringComparison.CurrentCulture));
    }

    private static string Nombre(ShopItem item) => string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;

    private void OnGUI()
    {
        PlayerLoadout equipo = Application.isPlaying ? Object.FindFirstObjectByType<PlayerLoadout>() : null;
        if (equipo == null) EditorGUILayout.HelpBox("Arrancá una partida (con el jugador en la escena) para darte armas.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(equipo == null))
        {
            Lista("Principales", principales, equipo, equipo != null ? equipo.Primary : null);
            EditorGUILayout.Space();
            Lista("Secundarias", secundarias, equipo, equipo != null ? equipo.Secondary : null);
            EditorGUILayout.Space();
            CuerpoACuerpo();
        }
        EditorGUILayout.EndScrollView();
    }

    private static void Lista(string titulo, List<ShopItem> armas, PlayerLoadout equipo, ShopItem enUso)
    {
        EditorGUILayout.LabelField(titulo, EditorStyles.boldLabel);
        if (armas == null) return;
        foreach (ShopItem arma in armas)
        {
            string texto = Nombre(arma) + (string.IsNullOrWhiteSpace(arma.alias) ? "" : $" ({arma.alias})") +
                           (arma == enUso ? "  · la tenés" : "");
            if (GUILayout.Button(texto)) equipo.Recibir(arma);
        }
    }

    // US 395: la katana (o volver al cuchillo), en el lugar del cuchillo. En Invasión queda como si la hubieras elegido
    // en el puesto (KatanaDeInvasion); en otro modo solo cambia el arma de ese lugar.
    private static void CuerpoACuerpo()
    {
        EditorGUILayout.LabelField("Cuerpo a cuerpo", EditorStyles.boldLabel);
        MeleeWeaponHolder lugar = Application.isPlaying ? Object.FindFirstObjectByType<MeleeWeaponHolder>() : null;
        KatanaData katana = KatanaData.Cargar();
        bool conKatana = lugar != null && lugar.CurrentWeapon is KatanaData;
        using (new EditorGUI.DisabledScope(lugar == null || lugar.CurrentWeapon == null))
        {
            if (katana != null && GUILayout.Button("Katana" + (conKatana ? "  · la tenés" : "")))
            {
                KatanaDeInvasion.Dar();
                if (!KatanaDeInvasion.Tiene) lugar.Cambiar(katana);
            }
            if (GUILayout.Button("Cuchillo" + (!conKatana ? "  · lo tenés" : "")))
            {
                KatanaDeInvasion.Quitar();
                lugar.Cambiar(lugar.DefaultWeapon);
            }
        }
    }

    private void OnInspectorUpdate() => Repaint();
}
