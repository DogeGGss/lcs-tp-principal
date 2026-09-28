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

    private IEnumerator NextTestRound()
    {
        yield return new WaitForSeconds(testRoundLength);
        Begin();
    }
}
