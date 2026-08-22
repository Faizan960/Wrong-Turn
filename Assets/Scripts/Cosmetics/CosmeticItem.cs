using UnityEngine;

namespace WrongDirection.Cosmetics
{
    public enum CosmeticCategory
    {
        ArrowSkin,
        BackgroundTheme,
        ParticleTheme,
        ComboTheme,
        UITheme
    }

    /// <summary>
    /// One purchasable/equippable visual. Strictly presentation: renderers
    /// read the payload fields (sprite, colors, particle prefab); nothing in
    /// gameplay ever branches on a cosmetic.
    /// HARD RULE (color clarity pass): cosmetics must never recolor a
    /// gameplay-critical rule surface — the tile tint, arrow glyph, rule glow
    /// halo and RuleColorLabel always show the rule palette (WHITE #FFFFFF /
    /// BLUE #168CFF / RED #FF3045 / YELLOW #FFD600 / EMERALD #00E676).
    /// Skins may only touch non-rule surfaces (backgrounds, particles, UI).
    /// Create via: Assets → Create → Wrong Turn → Cosmetic Item.
    /// </summary>
    [CreateAssetMenu(fileName = "CosmeticItem", menuName = "Wrong Turn/Cosmetic Item")]
    public class CosmeticItem : ScriptableObject
    {
        [Header("Identity")]
        public string id;                    // stable, saved in PlayerData — never rename after ship
        public string displayName;
        public CosmeticCategory category;

        [Header("Economy")]
        public int cost;
        public bool unlockedByDefault;

        [Header("Visual payload (renderers pick what they need)")]
        public Sprite sprite;                // arrow skin / UI icon
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.black;
        public GameObject particlePrefab;    // particle theme
        public TMPro.TMP_FontAsset font;     // UI / combo theme

        [Header("Shop presentation")]
        public Sprite previewImage;
        [TextArea] public string description;
    }
}
