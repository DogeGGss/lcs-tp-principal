using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Cosmetico = PerfilDatos.Cosmetico;
using Img = UnityEngine.UI.Image;
using Tipo = PerfilDatos.Tipo;

// US 376: el perfil de cada jugador en la tabla con Tab (Táctico y Deathmatch) y en la pantalla de resultado.
// - Cada jugador publica en la sala lo que tiene equipado (avatar, marco, banner y título), así todos ven lo mismo.
// - En la fila, el nombre va en una placa como la cabecera del Perfil: el banner oscurecido de fondo, el marco alrededor,
//   el avatar a la izquierda y el título debajo del nombre.
// - Si un jugador no publicó nada (por ejemplo, con una versión vieja), se ve con lo del conjunto inicial.
public static class PerfilEnPartida
{
    public const string Prop = "perfil";
    private const char Separador = '|';

    /// <summary>Publica lo equipado del jugador local. Fuera de una sala, Photon lo guarda y lo manda al entrar.</summary>
    public static void Publicar()
    {
        Player yo = PhotonNetwork.LocalPlayer;
        if (yo == null) return;
        string valor = string.Join(Separador.ToString(), Id(PerfilDatos.Avatar), Id(PerfilDatos.Marco), Id(PerfilDatos.Banner), Id(PerfilDatos.Titulo));
        if (yo.CustomProperties.TryGetValue(Prop, out object actual) && actual as string == valor) return;
        yo.SetCustomProperties(new Hashtable { { Prop, valor } });
    }

    private static string Id(Cosmetico c) => c != null ? c.id : "";

    /// <summary>Lo equipado de un jugador, con lo del conjunto inicial donde falte.</summary>
    public static (Cosmetico avatar, Cosmetico marco, Cosmetico banner, Cosmetico titulo) De(Player p)
    {
        if (p == null || p.IsLocal) return (PerfilDatos.Avatar, PerfilDatos.Marco, PerfilDatos.Banner, PerfilDatos.Titulo);
        string[] ids = p.CustomProperties.TryGetValue(Prop, out object valor) && valor is string texto ? texto.Split(Separador) : new string[0];
        string Parte(int i) => i < ids.Length ? ids[i] : null;
        return (Buscar(Tipo.Avatar, Parte(0), "riftwalker"), Buscar(Tipo.Marco, Parte(1), "anden-riftwalker"),
                Buscar(Tipo.Banner, Parte(2), "anden-riftwalker"), Buscar(Tipo.Titulo, Parte(3), ""));
    }

    private static Cosmetico Buscar(Tipo tipo, string id, string inicial) =>
        (id != null ? PerfilDatos.Buscar(tipo, id) : null) ?? PerfilDatos.Buscar(tipo, inicial);

    /// <summary>
    /// La placa del jugador en el recuadro (x, y, w, h) de su padre: banner, marco, avatar, nombre y título.
    /// apagada: jugador muerto o desconectado (todo más transparente).
    /// </summary>
    public static void Placa(RectTransform padre, float x, float y, float w, float h, Player p, string nombre,
        TMP_FontAsset fuenteNombre, TMP_FontAsset fuenteTitulo, Sprite redondeado, bool apagada)
    {
        var (avatar, marco, banner, titulo) = De(p);
        RectTransform placa = Place(Node("Perfil", padre), x, y, w, h);
        CanvasGroup grupo = placa.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = apagada ? 0.5f : 1f;
        grupo.blocksRaycasts = false;

        // Banner (prueba): de fondo, oscurecido para que se lea el nombre. Con marco, termina en el borde interior del riel.
        Vector4 d = PerfilUI.InteriorMarco(marco, h);
        bool conMarco = d != Vector4.zero;
        RectTransform caja = Place(Node("Banner", placa), d.x, d.y, w - d.x - d.z, h - d.y - d.w);
        if (!conMarco) Image(Stretch(Node("Fondo", caja)), redondeado, Rgb(20, 24, 32), 3f);
        PerfilUI.Cubrir(caja, PerfilDatos.Imagen(banner), 0.5f, 0.5f);
        Image(Stretch(Node("Oscuro", caja)), null, new Color(0f, 0f, 0f, 0.35f));
        Image(Stretch(Node("Sombra", caja)), PerfilUI.Degrade(true, 0.75f, 0.1f), Color.white);
        RectTransform marcoRect = null;
        PerfilUI.Enmarcar(ref marcoRect, placa, 0f, 0f, w, h, marco, false, caja);

        // Avatar con borde blanco, como en la cabecera del Perfil.
        float lado = h - 8f, ax = d.x + 6f, ay = (h - lado) / 2f;
        Image(Place(Node("BordeAvatar", placa), ax - 1.5f, ay - 1.5f, lado + 3f, lado + 3f), redondeado, Ink, 4f);
        PerfilUI.Cubrir(Place(Node("Avatar", placa), ax, ay, lado, lado), PerfilDatos.Imagen(avatar));

        // Nombre y, debajo, el título.
        float tx = ax + lado + 10f, tw = w - tx - d.z - 8f;
        bool sinTitulo = titulo == null || string.IsNullOrEmpty(titulo.id);
        TextMeshProUGUI n = Text(Place(Node("Nombre", placa), tx, sinTitulo ? 0f : 2f, tw, sinTitulo ? h : h * 0.56f),
            fuenteNombre, 19f, Ink, sinTitulo ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.BottomLeft, 2f);
        n.text = nombre;
        n.overflowMode = TextOverflowModes.Ellipsis;
        if (sinTitulo) return;
        TextMeshProUGUI t = Text(Place(Node("Titulo", placa), tx + 1f, h * 0.56f, tw, h * 0.4f - 2f),
            fuenteTitulo, 12f, Soft, TextAlignmentOptions.TopLeft, 3f);
        t.text = L10n.Source(titulo.nombre, "profile.");
        t.overflowMode = TextOverflowModes.Ellipsis;
    }
}
