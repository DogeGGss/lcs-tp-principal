using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas), US 381: el "calor" de un arma con skin legendaria: cada disparo enciende más su fuego y, sin
// disparar, se apaga de a poco (el shader lo lee en _Calor; SkinDragon.shader). Va en el modelo del arma (el de primera
// persona o el de la mano de los demás) y se agrega solo con el primer disparo. Es solo visual.
public class CalorDeSkin : MonoBehaviour
{
    private const float PorDisparo = 0.18f, Enfria = 0.5f; // de 0 a 1; por segundo

    private static readonly int Calor = Shader.PropertyToID("_Calor");
    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock bloque;
    private float calor, puesto = -1f;

    /// <summary>Esa arma disparó (si es null, nada).</summary>
    public static void Disparo(GameObject arma)
    {
        if (arma == null) return;
        CalorDeSkin c = arma.GetComponent<CalorDeSkin>();
        if (c == null) c = arma.AddComponent<CalorDeSkin>();
        c.calor = Mathf.Min(1f, c.calor + PorDisparo);
    }

    private void Update()
    {
        calor = Mathf.Max(0f, calor - Enfria * Time.deltaTime);
        if (Mathf.Abs(calor - puesto) < 0.005f) return;
        puesto = calor;
        bloque ??= new MaterialPropertyBlock();
        renderers.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (!(r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer)) renderers.Add(r);
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(bloque);
            bloque.SetFloat(Calor, calor);
            r.SetPropertyBlock(bloque);
        }
    }
}
