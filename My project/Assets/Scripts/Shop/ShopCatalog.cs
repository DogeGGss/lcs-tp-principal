using System.Collections.Generic;
using UnityEngine;

// Lo que se vende en un modo de juego, en el orden en que aparece en la tienda.
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Tienda/Catálogo")]
public class ShopCatalog : ScriptableObject
{
    [Tooltip("Arma secundaria que el jugador tiene siempre, sin pagar (US 078).")]
    public ShopItem starterSecondary;
    public List<ShopItem> items = new List<ShopItem>();

    public List<ShopItem> ItemsIn(ShopCategory category)
    {
        List<ShopItem> result = new List<ShopItem>();
        foreach (ShopItem item in items)
            if (item != null && item.category == category) result.Add(item);
        return result;
    }

    // Categorías que tienen algo para vender, en el orden del enum (US 076, CA3).
    public List<ShopCategory> ActiveCategories()
    {
        List<ShopCategory> result = new List<ShopCategory>();
        foreach (ShopCategory category in System.Enum.GetValues(typeof(ShopCategory)))
            if (ItemsIn(category).Count > 0) result.Add(category);
        return result;
    }
}
