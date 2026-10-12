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

    // Deathmatch (US 138): no hay plata. Las armas se eligen gratis y se conservan de una vida a la otra.
    public static bool Free => MatchSettings.Mode == GameMode.Deathmatch;

    // Al morir, justo antes de perder el equipamiento: así el arma todavía se puede soltar al piso (US 184, CA8).
    public event System.Action LosingEquipment;

    // El cambio que se está avisando es un arma levantada del piso (US 184): no se rellena ni se saca.
    public bool PickingUp { get; private set; }

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
        if (Free) StartCoroutine(DefaultEquipment());
    }

    // US 138, CA6: si no eligió nada, juega con el Mitre y La Porteña. Un cuadro después, para que el resto
    // (cambio de arma, tienda) ya esté escuchando el cambio.
    private System.Collections.IEnumerator DefaultEquipment()
    {
        yield return null;
        if (Primary != null || catalog == null) yield break;
        ShopItem rifle = catalog.items.Find(i => i != null && i.kind == ShopItemKind.PrimaryWeapon && i.alias == "Mitre");
        if (rifle == null) rifle = catalog.items.Find(i => i != null && i.kind == ShopItemKind.PrimaryWeapon);
        if (rifle == null) yield break;
        Primary = rifle;
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        if (phase != null) phase.Started -= OnPhaseStarted;
        if (health != null) health.Died -= LoseEquipment;
    }

    public int Count(ShopItem grenade) => grenade != null && grenades.TryGetValue(grenade, out int n) ? n : 0;
    public bool IsEquipped(ShopItem item) => item != null && (item == Primary || item == Secondary);
    public bool BoughtThisPhase(ShopItem item) => LastPurchaseOf(item) != null;

    // Granadas que tiene ahora, en el orden del catálogo (así la tecla 4 las recorre siempre igual).
    public List<ShopItem> OwnedGrenades()
    {
        List<ShopItem> owned = new List<ShopItem>();
        if (catalog == null) return owned;
        foreach (ShopItem item in catalog.ItemsIn(ShopCategory.Grenades))
            if (Count(item) > 0) owned.Add(item);
        return owned;
    }

    // Lanza una granada (US 079, CA6): se gasta una y esa compra ya no se puede vender ni deshacer.
    public bool Consume(ShopItem grenade)
    {
        if (grenade == null || grenade.kind != ShopItemKind.Grenade || Count(grenade) <= 0) return false;
        grenades[grenade] = Count(grenade) - 1;
        Purchase bought = LastPurchaseOf(grenade);
        if (bought != null) purchases.Remove(bought); // si no, "Deshacer" devolvería la plata de una granada ya usada
        Changed?.Invoke();
        return true;
    }

    // Si ahora se puede usar la tienda: fase de compra activa y dentro de la zona (US 076, CA9).
    public ShopResult CheckAccess()
    {
        // US 138, CA5: en Deathmatch no hay fase ni zona; se elige muerto, en la cuenta inicial o recién reaparecido.
        if (Free) return PartidaDeathmatch.PuedeElegir(out _, out _) ? ShopResult.Ok : ShopResult.BuyPhaseOver;
        if (BuyPhase.Current != null && !BuyPhase.Current.IsActive) return ShopResult.BuyPhaseOver;
        // En el Modo Zombie se compra en cualquier parte del mapa: no hay bases, aunque el mapa tenga zonas de compra.
        if (MatchSettings.Mode != GameMode.Zombie && !BuyZone.Contains(transform.position, EquiposTacticos.LadoLocal))
            return ShopResult.OutsideBuyZone;
        return ShopResult.Ok;
    }

    // Lo que se paga de verdad: el precio menos lo que se devuelve por reemplazar algo comprado en esta fase.
    public int CostOf(ShopItem item)
    {
        if (Free) return 0;
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
        if (Free)
        {
            // US 138, CA2 y CA3: solo armas, sin pagar.
            if (!item.IsWeapon) return ShopResult.NotSellable;
            return IsEquipped(item) ? ShopResult.AlreadyEquipped : ShopResult.Ok;
        }

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

        if (!Free) wallet.Spend(item.price);
        if (item.price > 0 && !Free)
            purchases.Add(new Purchase { item = item, paid = item.price, shieldBefore = shieldBefore });
        Changed?.Invoke();
        return ShopResult.Ok;
    }

    public ShopResult CanSell(ShopItem item)
    {
        if (Free) return ShopResult.NotSellable;
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

    // Suelta un arma al piso (US 184): su espacio queda vacío y esa compra ya no se puede vender ni deshacer (CA7).
    public bool Drop(ShopItem item)
    {
        if (item == null) return false;
        if (item == Primary) Primary = null;
        else if (item == Secondary) Secondary = null;
        else return false;
        Purchase bought = LastPurchaseOf(item);
        if (bought != null) purchases.Remove(bought);
        Changed?.Invoke();
        return true;
    }

    // Se puede levantar un arma del piso solo si su espacio (principal o secundaria) está vacío (US 184, CA5 y CA6).
    public bool CanPickUp(ShopItem item)
    {
        if (item == null) return false;
        if (item.kind == ShopItemKind.PrimaryWeapon) return Primary == null;
        if (item.kind == ShopItemKind.SecondaryWeapon) return Secondary == null;
        return false;
    }

    // Levanta un arma del piso: va a su espacio sin pagar y no cuenta como compra.
    public bool PickUp(ShopItem item)
    {
        if (!CanPickUp(item)) return false;
        if (item.kind == ShopItemKind.PrimaryWeapon) Primary = item; else Secondary = item;
        PickingUp = true;
        try { Changed?.Invoke(); }
        finally { PickingUp = false; }
        return true;
    }

    // US 195, CA3: en Deathmatch, el que vuelve a la partida recupera las armas que había elegido (su lugar en el
    // catálogo; menos de 0, ninguna).
    public void Restaurar(int primary, int secondary)
    {
        if (catalog == null) return;
        ShopItem principal = primary >= 0 && primary < catalog.items.Count ? catalog.items[primary] : null;
        ShopItem secundaria = secondary >= 0 && secondary < catalog.items.Count ? catalog.items[secondary] : null;
        if (principal == null && secundaria == null) return;
        if (principal != null && principal.kind == ShopItemKind.PrimaryWeapon) Primary = principal;
        if (secundaria != null && secundaria.kind == ShopItemKind.SecondaryWeapon) Secondary = secundaria;
        StartCoroutine(AvisarRestaurado());
    }

    // Un cuadro después, como DefaultEquipment: el cambio de arma y la tienda ya están escuchando.
    private System.Collections.IEnumerator AvisarRestaurado()
    {
        yield return null;
        Changed?.Invoke();
    }

    // Al morir se pierde todo menos la plata; se reaparece con el arma secundaria inicial.
    public void LoseEquipment()
    {
        if (Free) return; // US 138, CA4: lo elegido queda para las vidas siguientes
        LosingEquipment?.Invoke();
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
