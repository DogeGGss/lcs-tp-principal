using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// US 395: la katana del Modo Invasión. Lo agrega PartidaDeInvasion.
// - CA1 y CA2: se consigue en la elección del puesto (EleccionDeInvasion); desde ahí ocupa el lugar del cuchillo
//   (MeleeWeaponHolder.Cambiar) y queda toda la partida: si el jugador vuelve a aparecer con el cuchillo, se le vuelve a
//   poner. Se publica como propiedad del jugador en la sala (PropTiene), así los compañeros la ven en su mano y, al
//   volver a la partida (US 359), se recupera.
// - CA7: cada baja con la katana cura al jugador.
// - CA9: el brillo y la estela del filo (BrilloDeKatana), el destello y el sonido al frenar un disparo, y el sonido al
//   pegar. En cooperativo, la copia de cada compañero con la katana la tiene en la mano (JugadorEnRed), también en el
//   modo espectador (ParaEspectador).
public class KatanaDeInvasion : MonoBehaviourPunCallbacks
{
    public const string PropTiene = "inv.katana";
    private const float RevisarCada = 0.25f;

    private static KatanaDeInvasion actual;
    private bool tiene;
    private bool publicado, leida;
    private float proximaRevision;
    private MeleeWeaponHolder holder;
    private GameObject paraVerKatana, paraVerCuchillo; // CA9: los modelos que copia el espectador

    /// <summary>CA2: el jugador de esta computadora tiene la katana.</summary>
    public static bool Tiene => actual != null && actual.tiene;

    /// <summary>CA1: la eligió en el puesto.</summary>
    public static void Dar()
    {
        if (actual == null) return;
        actual.tiene = true;
        actual.proximaRevision = 0f;
        actual.Revisar();
    }

    /// <summary>Para probar (Riftwalker > Armas > Darme un arma...): la saca y vuelve el cuchillo.</summary>
    public static void Quitar()
    {
        if (actual == null) return;
        actual.tiene = false;
        actual.Publicar();
    }

    /// <summary>CA9: si ese jugador de la sala tiene la katana.</summary>
    public static bool TieneEl(Player p) => p != null && p.CustomProperties.TryGetValue(PropTiene, out object v) && v is bool b && b;

    public override void OnEnable()
    {
        base.OnEnable();
        actual = this;
        MeleeAttack.Mato += AlMatar;
        MeleeAttack.Freno += AlFrenar;
        MeleeAttack.PegoLaKatana += AlPegar;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (actual == this) actual = null;
        MeleeAttack.Mato -= AlMatar;
        MeleeAttack.Freno -= AlFrenar;
        MeleeAttack.PegoLaKatana -= AlPegar;
    }

    private void OnDestroy()
    {
        if (paraVerKatana != null) Destroy(paraVerKatana);
        if (paraVerCuchillo != null) Destroy(paraVerCuchillo);
    }

    private void Update()
    {
        if (Time.time < proximaRevision) return;
        proximaRevision = Time.time + RevisarCada;
        Revisar();
    }

    private void Revisar()
    {
        KatanaData katana = KatanaData.Cargar();
        if (katana == null) return;
        LeerAlVolver();
        ElPropio(katana);
        Publicar();
        if (CooperativoDeInvasion.Activo) LasDeLosCompaneros(katana);
    }

    // US 359: al volver a la partida se recupera (lo que publicó antes de irse); si no, se arranca sin ella (lo que
    // haya quedado de una partida anterior en la misma sala no cuenta).
    private void LeerAlVolver()
    {
        if (leida || !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        leida = true;
        if (Reconexion.Volvio && TieneEl(PhotonNetwork.LocalPlayer)) tiene = true;
        publicado = !tiene;
        Publicar(true);
    }

    private void Publicar(bool siempre = false)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null || (!siempre && tiene == publicado)) return;
        publicado = tiene;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropTiene, tiene } });
    }

    // CA2: mientras la tenga, la katana va en el lugar del cuchillo (también después de volver a aparecer). Su modelo
    // lleva el brillo y la estela (CA9).
    private void ElPropio(KatanaData katana)
    {
        if (holder == null)
        {
            PlayerLoadout equipo = FindAnyObjectByType<PlayerLoadout>();
            holder = equipo != null ? equipo.GetComponentInChildren<MeleeWeaponHolder>(true) : null;
            if (holder == null) holder = FindAnyObjectByType<MeleeWeaponHolder>();
        }
        if (holder == null || holder.CurrentWeapon == null) return; // (todavía no armó el cuchillo)
        if (tiene && holder.CurrentWeapon != katana) holder.Cambiar(katana);
        if (tiene && holder.CurrentViewModel != null && holder.CurrentViewModel.GetComponent<BrilloDeKatana>() == null)
            holder.CurrentViewModel.AddComponent<BrilloDeKatana>().Iniciar(holder.GetComponent<MeleeAttack>(), katana);
    }

    // CA9: la copia de cada compañero que tiene la katana la lleva en la mano.
    private static void LasDeLosCompaneros(KatanaData katana)
    {
        PartidaEnRed partida = PartidaEnRed.Actual;
        if (partida == null) return;
        foreach (JugadorEnRed j in partida.Jugadores)
            if (j != null && j.photonView != null && !j.photonView.IsMine && TieneEl(j.Dueno)) j.PonerCuerpoACuerpo(katana);
    }

    // CA7: cada baja con la katana cura.
    private void AlMatar(MeleeAttack ataque)
    {
        KatanaData katana = ataque != null ? ataque.Katana : null;
        if (katana == null || katana.curaPorBaja <= 0) return;
        HealthSystem vida = ataque.GetComponentInParent<HealthSystem>();
        if (vida == null) vida = ataque.transform.root.GetComponentInChildren<HealthSystem>();
        if (vida == null || vida.currentHealth <= 0) return;
        vida.SetState(vida.currentHealth + katana.curaPorBaja, vida.currentShield);
    }

    // CA9: al frenar un disparo, un destello del color de la energía, con chispas, y el choque metálico.
    private void AlFrenar(MeleeAttack ataque, Vector3 donde)
    {
        KatanaData katana = ataque != null ? ataque.Katana : null;
        Destellos.En(donde, new Color(0.35f, 0.75f, 1f), 6, 0.16f); // chico: es a un brazo de los ojos
        Sonar(ataque, SonidosDeKatana.Freno(), 0.9f);
        BrilloDeKatana brillo = ataque != null && ataque.GetComponent<MeleeWeaponHolder>() != null && ataque.GetComponent<MeleeWeaponHolder>().CurrentViewModel != null
            ? ataque.GetComponent<MeleeWeaponHolder>().CurrentViewModel.GetComponent<BrilloDeKatana>() : null;
        if (brillo != null) brillo.Destello();
    }

    // CA9: el golpe al tocar a alguien.
    private static void AlPegar(MeleeAttack ataque) => Sonar(ataque, SonidosDeKatana.Golpe(), 0.8f);

    private static void Sonar(MeleeAttack ataque, AudioClip clip, float volumen)
    {
        if (ataque == null || clip == null) return;
        AudioSource fuente = ataque.GetComponent<AudioSource>();
        if (fuente != null) fuente.PlayOneShot(clip, volumen);
    }

    /// <summary>CA9, modo espectador: el modelo de primera persona para mostrar el arma cuerpo a cuerpo del compañero
    /// que se mira, si no es la misma que la de este jugador (él tiene la katana y este no, o al revés). Si es la misma,
    /// "propio" (el del jugador de esta computadora).</summary>
    public static GameObject ParaEspectador(bool companeroConKatana, GameObject propio)
    {
        if (actual == null) return propio;
        MeleeWeaponHolder h = actual.holder;
        if (h == null || h.ViewModelParent == null) return propio;
        bool yoConKatana = h.CurrentWeapon is KatanaData;
        if (companeroConKatana == yoConKatana) return propio;
        if (companeroConKatana)
            return actual.ParaVer(ref actual.paraVerKatana, KatanaData.Cargar(), h.ViewModelParent);
        return actual.ParaVer(ref actual.paraVerCuchillo, h.DefaultWeapon, h.ViewModelParent);
    }

    // Una copia apagada del modelo de primera persona de esa arma, en el mismo lugar que el del jugador.
    private GameObject ParaVer(ref GameObject modelo, MeleeWeaponData arma, Transform padre)
    {
        if (modelo != null) return modelo;
        if (arma == null || arma.viewModelPrefab == null) return null;
        modelo = Instantiate(arma.viewModelPrefab, padre, false);
        modelo.name = arma.viewModelPrefab.name + " (para el espectador)";
        modelo.SetActive(false);
        // Sin agarres: los brazos de primera persona (BrazosEnCamara) no le sacan foto.
        foreach (string agarre in new[] { BrazosEnCamara.AgarreDerecho, BrazosEnCamara.AgarreIzquierdo })
        {
            Transform t = modelo.transform.Find(agarre);
            if (t != null) Destroy(t.gameObject);
        }
        return modelo;
    }
}
