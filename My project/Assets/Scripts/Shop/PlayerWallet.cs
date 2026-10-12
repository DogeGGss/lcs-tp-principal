using UnityEngine;

// Dinero del jugador en el modo Táctico (F20). Las recompensas por ronda y por
// eliminación se suman con Add() cuando exista el sistema de rondas.
public class PlayerWallet : MonoBehaviour
{
    [SerializeField] private int startingMoney = 800;
    [SerializeField] private int maxMoney = 9000;

    public int Money { get; private set; }
    public event System.Action<int> MoneyChanged;

    private void Awake()
    {
        Money = Mathf.Clamp(startingMoney, 0, maxMoney);
    }

    public bool CanAfford(int amount) => amount <= Money;

    public bool Spend(int amount)
    {
        if (amount < 0 || amount > Money) return false;
        Money -= amount;
        MoneyChanged?.Invoke(Money);
        return true;
    }

    /// <summary>US 032: la plata vuelve a un monto fijo (cambio de lado: $ 800; muerte súbita: $ 5.000).</summary>
    public void Set(int amount)
    {
        Money = Mathf.Clamp(amount, 0, maxMoney);
        MoneyChanged?.Invoke(Money);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Money = Mathf.Min(maxMoney, Money + amount);
        MoneyChanged?.Invoke(Money);
    }
}
