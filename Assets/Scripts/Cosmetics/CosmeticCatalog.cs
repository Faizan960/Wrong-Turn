using UnityEngine;

namespace WrongDirection.Cosmetics
{
    /// <summary>
    /// The full cosmetic inventory as one asset, loaded by CosmeticManager
    /// from Resources/CosmeticCatalog. Keeping it a single SO (instead of a
    /// Resources folder scan) makes the load deterministic and lets the shop
    /// UI iterate in authored order.
    /// </summary>
    [CreateAssetMenu(fileName = "CosmeticCatalog", menuName = "Wrong Turn/Cosmetic Catalog")]
    public class CosmeticCatalog : ScriptableObject
    {
        public CosmeticItem[] items;

        /// <summary>Linear scan — catalog is small (dozens), called from UI/equip only.</summary>
        public CosmeticItem FindById(string id)
        {
            if (items == null) return null;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].id == id)
                    return items[i];
            return null;
        }
    }
}
