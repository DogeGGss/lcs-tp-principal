using System.Collections;
using UnityEngine;

// Fase de compra: los primeros segundos de cada ronda, cuando se puede usar la tienda (US 076).
// Si la escena no tiene una fase de compra, la tienda permite comprar siempre.
public class BuyPhase : MonoBehaviour
{
    public static BuyPhase Current { get; private set; }

    [SerializeField] private float duration = 20f;
    [SerializeField] private bool startOnPlay = true;
    [Tooltip("Solo para probar sin sistema de rondas: segundos que dura la \"ronda\" antes de volver a abrir la compra. 0 = no se repite.")]
    [SerializeField] private float testRoundLength = 0f;

    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }
    public float Duration => duration;
    public int Round { get; private set; }

    public event System.Action Started;
    public event System.Action Ended;

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Start()
    {
        if (startOnPlay) Begin();
    }

    public void Begin()
    {
        Round++;
        TimeLeft = duration;
        IsActive = true;
        Started?.Invoke();
    }

    private void Update()
    {
        if (!IsActive) return;
        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;

        TimeLeft = 0f;
        IsActive = false;
        Ended?.Invoke();
        if (testRoundLength > 0f) StartCoroutine(NextTestRound());
    }

    /// <summary>
    /// Modo Táctico (US 032): las rondas las lleva RondasTacticas y acá solo se copia el estado. Si cambia la ronda
    /// o la compra estaba cerrada, se avisa Started (la tienda deja de poder vender lo de la ronda anterior).
    /// </summary>
    public void Sincronizar(int ronda, float restante)
    {
        StopAllCoroutines();
        testRoundLength = 0f;
        TimeLeft = Mathf.Max(0f, restante);
        if (IsActive && Round == ronda) return;
        Round = ronda;
        IsActive = true;
        Started?.Invoke();
    }

    /// <summary>US 032: cierra la compra ya (empieza el combate).</summary>
    public void Terminar()
    {
        if (!IsActive) return;
        TimeLeft = 0f;
        IsActive = false;
        Ended?.Invoke();
    }

    /// <summary>US 032: número de ronda sin abrir la compra (por ejemplo, al entrar con la ronda ya empezada).</summary>
    public void PonerRonda(int ronda) => Round = ronda;

    private IEnumerator NextTestRound()
    {
        yield return new WaitForSeconds(testRoundLength);
        Begin();
    }
}
