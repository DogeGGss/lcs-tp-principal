using System.Collections.Generic;
using UnityEngine;

public enum ShopResult { Ok, NotEnoughMoney, AlreadyEquipped, MaxReached, ShieldFull, BuyPhaseOver, OutsideBuyZone, NotSellable }

// Equipamiento del jugador y reglas de compra y venta de la tienda (US 077 a US 080).
[RequireComponent(typeof(PlayerWallet))]
public class PlayerLoadout : MonoBehaviour
{
    [SerializeField] private ShopCatalog catalog;

    private class Purchase
    {
        public ShopItem item;
        public int paid;
        public int shieldBefore; // escudo que tenía antes de comprar escudo en esta fase
    }

    private readonly List<Purchase> purchases = new List<Purchase>();
    private readonly Dictionary<ShopItem, int> grenades = new Dictionary<ShopItem, int>();
    private PlayerWallet wallet;
    private HealthSystem health;
    private BuyPhase phase;

    public ShopCatalog Catalog => catalog;
    public PlayerWallet Wallet => wallet;
    public ShopItem Primary { get; private set; }
    public ShopItem Secondary { get; private set; }
    public int Shield => health != null ? health.currentShield : 0;
    public bool HasPurchases => purchases.Count > 0;

    public event System.Action Changed;

    public int SpentThisPhase
    {
        get
        {
            int total = 0;
            foreach (Purchase purchase in purchases) total += purchase.paid;
            return total;
        }
    }

    private void Awake()
    {
        wallet = GetComponent<PlayerWallet>();
        health = GetComponent<HealthSystem>();
        Secondary = catalog != null ? catalog.starterSecondary : null;
    }

    private void Start()
    {
        phase = BuyPhase.Current;
        if (phase != null) phase.Started += OnPhaseStarted;
        if (health != null) health.Died += LoseEquipment;
    }

    private void OnDestroy()
    {
        if (phase != null) phase.Started -= OnPhaseStarted;
        if (health != null) health.Died -= LoseEquipment;
    }

    public int Count(ShopItem grenade) => grenade != null && grenades.TryGetValue(grenade, out int n) ? n : 0;
    public bool IsEquipped(ShopItem item) => item != null && (item == Primary || item == Secondary);
    public bool BoughtThisPhase(ShopItem item) => LastPurchaseOf(item) != null;

    // Si ahora se puede usar la tienda: fase de compra activa y dentro de la zona (US 076, CA9).
    public ShopResult CheckAccess()
    {
        if (BuyPhase.Current != null && !BuyPhase.Current.IsActive) return ShopResult.BuyPhaseOver;
        if (!BuyZone.Contains(transform.position)) return ShopResult.OutsideBuyZone;
        return ShopResult.Ok;
    }

    // Lo que se paga de verdad: el precio menos lo que se devuelve por reemplazar algo comprado en esta fase.
    public int CostOf(ShopItem item)
    {
        int refund = 0;
        if (item.kind == ShopItemKind.PrimaryWeapon) refund = PaidThisPhase(Primary);
        else if (item.kind == ShopItemKind.SecondaryWeapon) refund = PaidThisPhase(Secondary);
        else if (item.kind == ShopItemKind.Shield) refund = ShieldPurchase()?.paid ?? 0;
        return item.price - refund;
    }

    public ShopResult CanBuy(ShopItem item)
    {
        ShopResult access = CheckAccess();
        if (access != ShopResult.Ok) return access;

        switch (item.kind)
        {
            case ShopItemKind.PrimaryWeapon:
            case ShopItemKind.SecondaryWeapon:
                if (IsEquipped(item)) return ShopResult.AlreadyEquipped;
                break;
            case ShopItemKind.Shield:
                if (Shield >= item.shieldPoints) return ShopResult.ShieldFull;
                break;
            case ShopItemKind.Grenade:
                if (Count(item) >= item.maxCarry) return ShopResult.MaxReached;
                break;
        }
        return CostOf(item) > wallet.Money ? ShopResult.NotEnoughMoney : ShopResult.Ok;
    }

    public ShopResult Buy(ShopItem item)
    {
        ShopResult result = CanBuy(item);
        if (result != ShopResult.Ok) return result;

        int shieldBefore = Shield;
        switch (item.kind)
        {
            case ShopItemKind.PrimaryWeapon:
                RefundIfBoughtThisPhase(Primary);
                Primary = item;
                break;
            case ShopItemKind.SecondaryWeapon:
                RefundIfBoughtThisPhase(Secondary);
                Secondary = item;
                break;
            case ShopItemKind.Shield:
                Purchase previous = ShieldPurchase();
                if (previous != null)
                {
                    // Mejora en la misma fase: se devuelve el escudo anterior (US 080, CA3).
                    shieldBefore = previous.shieldBefore;
                    purchases.Remove(previous);
                    wallet.Add(previous.paid);
                }
                if (health != null) health.SetShield(item.shieldPoints);
                break;
            case ShopItemKind.Grenade:
                grenades[item] = Count(item) + 1;
                break;
        }

        wallet.Spend(item.price);
        if (item.price > 0)
            purchases.Add(new Purchase { item = item, paid = item.price, shieldBefore = shieldBefore });
        Changed?.Invoke();
        return ShopResult.Ok;
    }

    public ShopResult CanSell(ShopItem item)
    {
        ShopResult access = CheckAccess();
        if (access != ShopResult.Ok) return access;

        Purchase purchase = LastPurchaseOf(item);
        if (purchase == null) return ShopResult.NotSellable;
        if (item.IsWeapon && !IsEquipped(item)) return ShopResult.NotSellable;
        // Un escudo que ya recibió daño no se vende (US 080, CA7).
        if (item.kind == ShopItemKind.Shield && Shield < item.shieldPoints) return ShopResult.NotSellable;
        return ShopResult.Ok;
    }

    public ShopResult Sell(ShopItem item)
    {
        ShopResult result = CanSell(item);
        if (result != ShopResult.Ok) return result;
        Revert(LastPurchaseOf(item));
        Changed?.Invoke();
        return ShopResult.Ok;
    }

    // Botón "Deshacer": devuelve todo lo comprado en la fase actual (US 076, CA10).
    public void UndoPurchases()
    {
        if (CheckAccess() != ShopResult.Ok) return;
        for (int i = purchases.Count - 1; i >= 0; i--) Revert(purchases[i]);
        Changed?.Invoke();
    }

    // Al morir se pierde todo menos la plata; se reaparece con el arma secundaria inicial.
    public void LoseEquipment()
    {
        Primary = null;
        Secondary = catalog != null ? catalog.starterSecondary : null;
        grenades.Clear();
        purchases.Clear();
        Changed?.Invoke();
    }

    private void OnPhaseStarted()
    {
        // Lo comprado en la fase anterior ya no se puede vender ni deshacer.
        purchases.Clear();
        Changed?.Invoke();
    }

    private void Revert(Purchase purchase)
    {
        purchases.Remove(purchase);
        wallet.Add(purchase.paid);
        switch (purchase.item.kind)
        {
            case ShopItemKind.PrimaryWeapon:
                if (Primary == purchase.item) Primary = null;
                break;
            case ShopItemKind.SecondaryWeapon:
                if (Secondary == purchase.item) Secondary = catalog != null ? catalog.starterSecondary : null;
                break;
            case ShopItemKind.Shield:
                if (health != null) health.SetShield(purchase.shieldBefore);
                break;
            case ShopItemKind.Grenade:
                grenades[purchase.item] = Mathf.Max(0, Count(purchase.item) - 1);
                break;
        }
    }

    private void RefundIfBoughtThisPhase(ShopItem item)
    {
        Purchase purchase = LastPurchaseOf(item);
        if (purchase == null) return;
        purchases.Remove(purchase);
        wallet.Add(purchase.paid);
    }

    private int PaidThisPhase(ShopItem item) => LastPurchaseOf(item)?.paid ?? 0;

    private Purchase ShieldPurchase() => purchases.FindLast(p => p.item.kind == ShopItemKind.Shield);

    private Purchase LastPurchaseOf(ShopItem item) => item == null ? null : purchases.FindLast(p => p.item == item);
}
