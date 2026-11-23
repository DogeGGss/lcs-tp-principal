using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas): lo que las skins necesitan del proyecto. Es un asset (Resources/Skins/ModelosDeArmas):
// - US 379: el modelo 3D de cada arma (el del FBX, sin scripts), para la vista previa de las skins en el perfil, donde no
//   está el jugador con sus armas; el id del arma es el del catálogo (CatalogoDeSkins.Armas).
// - El material de cada diseño (CatalogoDeSkins.Disenos), por su id.
// - US 381: el ajuste de la boca de cada arma en la pantalla (de dónde salen el fogonazo y la estela de las legendarias
//   en primera persona), que se acomoda a ojo desde Riftwalker > Armas > Skins de prueba.
[CreateAssetMenu(menuName = "Riftwalker/Modelos de las armas (skins)")]
public class ModelosDeArmas : ScriptableObject
{
    [System.Serializable]
    public class Modelo
    {
        [Tooltip("El id del arma en CatalogoDeSkins (mitre, urquiza...).")]
        public string arma = "";
        [Tooltip("El modelo del arma (el FBX).")]
        public GameObject modelo;
    }

    [System.Serializable]
    public class MaterialDeDiseno
    {
        [Tooltip("El id del diseño en CatalogoDeSkins (codigo-binario...).")]
        public string diseno = "";
        public Material material;
    }

    [System.Serializable]
    public class AjusteDeBoca
    {
        public string arma = "";
        [Tooltip("Cuánto se corre la boca en la pantalla (en altos de pantalla): x a la derecha, y para arriba.")]
        public Vector2 corrimiento;
    }

    public List<Modelo> modelos = new List<Modelo>();
    public List<AjusteDeBoca> bocas = new List<AjusteDeBoca>();
    public List<MaterialDeDiseno> materiales = new List<MaterialDeDiseno>();

    private static ModelosDeArmas actual;

    /// <summary>El asset de Resources (o uno vacío, si falta).</summary>
    public static ModelosDeArmas Actual
    {
        get
        {
            if (actual != null) return actual;
            actual = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
            if (actual == null) actual = CreateInstance<ModelosDeArmas>();
            return actual;
        }
    }

    /// <summary>El modelo de esa arma, o null.</summary>
    public GameObject De(string arma) => modelos.Find(m => m != null && m.arma == arma)?.modelo;

    /// <summary>US 381: cuánto se corre la boca de esa arma en la pantalla (en altos de pantalla).</summary>
    public Vector2 CorrimientoDeBoca(string arma) => bocas.Find(b => b != null && b.arma == arma)?.corrimiento ?? Vector2.zero;

    /// <summary>US 381: lo cambia (Skins de prueba).</summary>
    public void CorrerBoca(string arma, Vector2 corrimiento)
    {
        AjusteDeBoca b = bocas.Find(x => x != null && x.arma == arma);
        if (b == null) bocas.Add(b = new AjusteDeBoca { arma = arma });
        b.corrimiento = corrimiento;
    }

    /// <summary>El material de ese diseño, o null.</summary>
    public Material MaterialDe(string diseno) => materiales.Find(m => m != null && m.diseno == diseno)?.material;
}
