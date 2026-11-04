using UnityEngine;

// Las balas de un arma cualquiera de las tres que hay (ArmaDeFuego, el Mitre y La Porteña), para las cartas de Invasión
// que dan munición: Reciclador (US 316: balas al cargador), Filo (US 317: recarga al instante) y Carroñero (US 318: un
// cargador).
public static class MunicionDelArma
{
    /// <summary>Suma balas al cargador sin pasarse de lleno; lo que sobra va a la reserva (sin pasarse de la llena).</summary>
    public static void SumarAlCargador(GameObject arma, int balas)
    {
        if (arma == null || balas <= 0) return;
        if (arma.TryGetComponent(out ArmaDeFuego fuego) && fuego.MagazineSize > 0)
        {
            int entra = Mathf.Clamp(fuego.MagazineSize - fuego.currentAmmo, 0, balas);
            fuego.currentAmmo += entra;
            fuego.reserveAmmo = Mathf.Max(fuego.reserveAmmo, Mathf.Min(fuego.ReservaLlena, fuego.reserveAmmo + balas - entra));
        }
        else if (arma.TryGetComponent(out Mitre mitre))
        {
            int entra = Mathf.Clamp(mitre.maxAmmo - mitre.currentAmmo, 0, balas);
            mitre.currentAmmo += entra;
            mitre.reserveAmmo = Mathf.Max(mitre.reserveAmmo, Mathf.Min(mitre.ReservaLlena, mitre.reserveAmmo + balas - entra));
        }
        else if (arma.TryGetComponent(out Pistola pistola))
        {
            int entra = Mathf.Clamp(pistola.maxAmmo - pistola.currentAmmo, 0, balas);
            pistola.currentAmmo += entra;
            pistola.reserveAmmo = Mathf.Max(pistola.reserveAmmo, Mathf.Min(pistola.ReservaLlena, pistola.reserveAmmo + balas - entra));
        }
    }

    /// <summary>Llena el cargador desde la reserva al instante, como si hubiera terminado de recargar.</summary>
    public static void LlenarCargador(GameObject arma)
    {
        if (arma == null) return;
        if (arma.TryGetComponent(out ArmaDeFuego fuego) && fuego.MagazineSize > 0)
            Pasar(ref fuego.currentAmmo, ref fuego.reserveAmmo, fuego.MagazineSize);
        else if (arma.TryGetComponent(out Mitre mitre)) Pasar(ref mitre.currentAmmo, ref mitre.reserveAmmo, mitre.maxAmmo);
        else if (arma.TryGetComponent(out Pistola pistola)) Pasar(ref pistola.currentAmmo, ref pistola.reserveAmmo, pistola.maxAmmo);
    }

    private static void Pasar(ref int cargador, ref int reserva, int lleno)
    {
        int pasan = Mathf.Min(Mathf.Max(0, lleno - cargador), reserva);
        cargador += pasan;
        if (!Municion.ReservaInfinita) reserva -= pasan;
    }

    /// <summary>Un cargador entero del arma (a la reserva; si está llena, al cargador).</summary>
    public static void SumarUnCargador(GameObject arma)
    {
        if (arma == null) return;
        if (arma.TryGetComponent(out ArmaDeFuego fuego) && fuego.MagazineSize > 0) fuego.SumarBalas(fuego.MagazineSize);
        else if (arma.TryGetComponent(out Mitre mitre)) mitre.SumarBalas(mitre.maxAmmo);
        else if (arma.TryGetComponent(out Pistola pistola)) pistola.SumarBalas(pistola.maxAmmo);
    }

    /// <summary>El arma de fuego que tiene en la mano; con el cuchillo o una granada, la última que tuvo (si todavía la
    /// tiene); si no, la principal o la secundaria.</summary>
    public static GameObject EnLaMano(WeaponSwitcher armas)
    {
        if (armas == null) return null;
        GameObject enLaMano = armas.HeldPrimary ?? armas.HeldSecondary;
        if (enLaMano != null) return enLaMano;
        GameObject ultima = armas.UltimaDeFuego;
        if (ultima != null && (ultima == armas.PrimaryObj || ultima == armas.SecondaryObj)) return ultima;
        return armas.PrimaryObj ?? armas.SecondaryObj;
    }
}
