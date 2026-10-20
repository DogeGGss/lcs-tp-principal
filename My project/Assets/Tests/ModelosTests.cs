using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Modelos de los personajes (US 225). CuerpoDePersonaje pone el modelo de cada uno en el cuerpo del jugador, y las zonas
// de daño del prefab (cabeza, cuerpo y piernas) tienen que pasar a los huesos del modelo nuevo. Se arma solo el cuerpo
// del prefab del jugador (animador, malla, huesos y zonas), sin cámara, red ni controles.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class ModelosTests
{
    private static readonly string[] Personajes = { "Alfa", "Bravo", "Charlie", "Delta" };
    private GameObject cuerpo;

    [TearDown]
    public void Limpiar()
    {
        if (cuerpo != null) Object.Destroy(cuerpo);
    }

    [UnityTest, Category("US 225"), Description("TC-994 CA6: las zonas de daño siguen al modelo y un tiro a la cabeza es cabeza")]
    public IEnumerator Las_zonas_de_danio_siguen_al_modelo([ValueSource(nameof(Personajes))] string personaje)
    {
        CharacterData datos = Resources.Load<CharacterData>("Personajes/" + personaje);
        Assert.IsNotNull(datos, "Falta Resources/Personajes/" + personaje);
        Assert.IsNotNull(datos.modelo, personaje + " no tiene modelo");
#if UNITY_EDITOR
        GameObject jugador = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
#else
        GameObject jugador = null;
        Assert.Ignore("Solo en el editor: el prefab del jugador se lee del proyecto.");
#endif
        Assert.IsNotNull(jugador, "Falta Assets/Prefabs/Player.prefab");

        cuerpo = Object.Instantiate(jugador.GetComponentInChildren<Animator>(true).gameObject);
        Animator animador = cuerpo.GetComponent<Animator>();
        animador.fireEvents = false; // las animaciones avisan pasos al jugador, que acá no está
        int zonas = cuerpo.GetComponentsInChildren<HitZone>(true).Length;
        Assert.Greater(zonas, 0, "el cuerpo del jugador no tiene zonas de daño");
        yield return null;

        Assert.IsTrue(CuerpoDePersonaje.Cambiar(animador, datos.modelo), "el modelo de " + personaje + " no es humanoide");
        yield return null; // se va el cuerpo viejo
        yield return null; // el animador pone la pose del modelo nuevo

        HitZone[] despues = cuerpo.GetComponentsInChildren<HitZone>(true);
        Assert.AreEqual(zonas, despues.Length, "se perdió alguna zona al cambiar el modelo");
        Transform cadera = animador.GetBoneTransform(HumanBodyBones.Hips);
        foreach (HitZone z in despues)
            Assert.IsTrue(z.transform.IsChildOf(cadera), z.transform.parent.name + " no es un hueso del modelo nuevo");

        HitZone cabeza = System.Array.Find(despues, z => z.zone == BodyZone.Head);
        Assert.IsNotNull(cabeza, "no hay zona de cabeza");
        Assert.AreEqual(animador.GetBoneTransform(HumanBodyBones.Head), cabeza.transform.parent, "la cabeza no está en el hueso de la cabeza");

        // Un tiro de frente a la altura de la cabeza encuentra primero la zona de la cabeza, como en WeaponFire.
        Physics.SyncTransforms();
        Vector3 centro = cabeza.GetComponent<Collider>().bounds.center;
        Vector3 frente = cuerpo.transform.forward;
        RaycastHit[] golpes = Physics.RaycastAll(centro + frente * 3f, -frente, 6f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));
        HitZone primera = null;
        foreach (RaycastHit golpe in golpes)
        {
            primera = golpe.collider.GetComponent<HitZone>();
            if (primera != null) break;
        }
        Assert.IsNotNull(primera, "el tiro no encontró ninguna zona");
        Assert.AreEqual(BodyZone.Head, primera.zone, "el tiro a la cabeza pegó primero en " + primera.transform.parent.name);
    }
}
