using System.Collections.Generic;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Cosmetics;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Unlock / equip / purchase state for cosmetics. Visual consumers
    /// (HUD, backgrounds, particles) call EquippedFor(category) and listen to
    /// OnCosmeticEquipped to re-skin — this manager never touches renderers,
    /// and nothing here affects gameplay.
    ///
    /// Purchases go through CurrencyManager.TrySpend — the one sanctioned
    /// economy API. Persistence stays in SaveManager (id lists in PlayerData).
    /// </summary>
    public class CosmeticManager : MonoSingleton<CosmeticManager>
    {
        public CosmeticCatalog Catalog { get; private set; }

        private readonly HashSet<string> _unlocked = new HashSet<string>();
        // category → equipped item, resolved once at load
        private readonly Dictionary<CosmeticCategory, CosmeticItem> _equipped =
            new Dictionary<CosmeticCategory, CosmeticItem>();

        protected override void OnSingletonAwake()
        {
            Catalog = Resources.Load<CosmeticCatalog>("CosmeticCatalog");
            if (Catalog == null)
            {
                Debug.LogWarning("[CosmeticManager] No Resources/CosmeticCatalog asset — cosmetics disabled.");
                return;
            }

            var data = SaveManager.Instance.Data;

            foreach (string id in data.unlockedCosmetics)
                _unlocked.Add(id);

            // Defaults are always unlocked.
            for (int i = 0; i < Catalog.items.Length; i++)
                if (Catalog.items[i] != null && Catalog.items[i].unlockedByDefault)
                    _unlocked.Add(Catalog.items[i].id);

            foreach (string id in data.equippedCosmetics)
            {
                var item = Catalog.FindById(id);
                if (item != null && _unlocked.Contains(id))
                    _equipped[item.category] = item;
            }
        }

        private void OnEnable()  => GameEvents.OnChallengeEnded += HandleChallengeEnded;
        private void OnDisable() => GameEvents.OnChallengeEnded -= HandleChallengeEnded;

        /// <summary>Daily challenge cosmetic rewards unlock here, off the bus.</summary>
        private void HandleChallengeEnded(DailyChallengeData challenge, bool completed)
        {
            if (!completed || string.IsNullOrEmpty(challenge.cosmeticRewardId) || Catalog == null) return;
            Unlock(Catalog.FindById(challenge.cosmeticRewardId));
        }

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        public CosmeticItem EquippedFor(CosmeticCategory category) =>
            _equipped.TryGetValue(category, out var item) ? item : null;

        /// <summary>Buy with coins, then unlock. False if unaffordable/unknown/owned.</summary>
        public bool TryPurchase(string id)
        {
            var item = Catalog != null ? Catalog.FindById(id) : null;
            if (item == null || _unlocked.Contains(id)) return false;
            if (!CurrencyManager.Instance.TrySpend(item.cost)) return false;

            Unlock(item);
            return true;
        }

        /// <summary>Unlock without payment (daily challenge / achievement rewards).</summary>
        public void Unlock(CosmeticItem item)
        {
            if (item == null || !_unlocked.Add(item.id)) return;
            SaveManager.Instance.Data.unlockedCosmetics.Add(item.id);
            SaveManager.Instance.Save();
        }

        /// <summary>Equip an unlocked item (replaces whatever holds its category).</summary>
        public bool Equip(string id)
        {
            var item = Catalog != null ? Catalog.FindById(id) : null;
            if (item == null || !_unlocked.Contains(id)) return false;

            _equipped[item.category] = item;
            SyncEquippedList();
            SaveManager.Instance.Save();
            GameEvents.RaiseCosmeticEquipped(id);
            return true;
        }

        private void SyncEquippedList()
        {
            var list = SaveManager.Instance.Data.equippedCosmetics;
            list.Clear();
            foreach (var kv in _equipped)
                list.Add(kv.Value.id);
        }
    }
}
