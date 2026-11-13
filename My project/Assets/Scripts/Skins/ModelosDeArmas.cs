using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas): lo que las skins necesitan del proyecto. Es un asset (Resources/Skins/ModelosDeArmas):
// - US 379: el modelo 3D de cada arma (el del FBX, sin scripts), para la vista previa de las skins en el perfil, donde no
//   está el jugador con sus armas; el id del arma es el del catálogo (CatalogoDeSkins.Armas).
// - El material de cada diseño (CatalogoDeSkins.Disenos), por su id.
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

    public List<Modelo> modelos = new List<Modelo>();
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

    /// <summary>El material de ese diseño, o null.</summary>
    public Material MaterialDe(string diseno) => materiales.Find(m => m != null && m.diseno == diseno)?.material;
}
