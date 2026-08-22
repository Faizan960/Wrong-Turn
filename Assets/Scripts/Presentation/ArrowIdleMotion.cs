using UnityEngine;
using UnityEngine.UI;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Living-tile idle motion (Phase 5 Task 1): floats a pivot 0 → -8 → 0 px
    /// over 3s forever, optionally breathes a glow's alpha and the pivot's
    /// scale. Drives a dedicated pivot ABOVE the tile so it never fights the
    /// entrance/punch/beat tweens that own the tile's own transform. Also keeps
    /// an optional shadow's visibility in sync with the tile (GameplayHUD hides
    /// only the arrow itself between runs). Unscaled time: idles through
    /// hitstop and pause.
    /// </summary>
    public class ArrowIdleMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform target;      // the float pivot
        [SerializeField] private float amplitude = 8f;      // px, dips downward
        [SerializeField] private float period = 3f;

        [Header("Optional ambient glow breathing")]
        [SerializeField] private Image glow;
        [SerializeField] private float glowMinAlpha = 0.12f;
        [SerializeField] private float glowMaxAlpha = 0.22f;
        [SerializeField] private float glowPeriod = 2f;

        [Header("Optional scale breathing (menu tile)")]
        [SerializeField] private float breatheScale;        // 0 = off, e.g. 0.015

        [Header("Micro rotation (Phase 5 Part 1 Layer 2)")]
        [SerializeField] private float rotationDegrees = 1f;   // ±, 0 = off
        [SerializeField] private float rotationPeriod = 3.7f;  // deliberately off-beat vs. the float

        [Header("Optional shadow synced to the tile's visibility")]
        [SerializeField] private GameObject shadow;
        [SerializeField] private GameObject visual;         // the tile the shadow belongs to

        private Vector2 _home;
        private bool _homeCached;

        private void OnEnable()
        {
            if (target != null)
            {
                _home = target.anchoredPosition;
                _homeCached = true;
            }
        }

        private void OnDisable()
        {
            if (target != null && _homeCached)
            {
                target.anchoredPosition = _home;
                target.localScale = Vector3.one;
                target.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (AccessibilityPrefs.ReduceMotion)
            {
                if (target != null && _homeCached)
                {
                    target.anchoredPosition = _home;
                    target.localScale = Vector3.one;
                    target.localRotation = Quaternion.identity;
                }
                if (shadow != null && visual != null && shadow.activeSelf != visual.activeSelf)
                    shadow.SetActive(visual.activeSelf);
                return;
            }

            if (target != null && _homeCached)
            {
                // Cosine dip: starts and ends at 0, bottoms out at -amplitude.
                float t = Mathf.Repeat(now, period) / period;
                float y = -amplitude * 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI));
                target.anchoredPosition = new Vector2(_home.x, _home.y + y);

                if (breatheScale > 0f)
                {
                    float b = 0.5f * (1f - Mathf.Cos(Mathf.Repeat(now, glowPeriod) / glowPeriod * 2f * Mathf.PI));
                    target.localScale = Vector3.one * (1f + breatheScale * b);
                }

                if (rotationDegrees > 0f)
                {
                    // Two off-beat sines sum to a wander that never visibly repeats.
                    float wander = Mathf.Sin(now * 2f * Mathf.PI / rotationPeriod)
                                 + 0.5f * Mathf.Sin(now * 2f * Mathf.PI / (rotationPeriod * 1.618f));
                    target.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees * wander / 1.5f);
                }
            }

            if (glow != null && glow.gameObject.activeInHierarchy)
            {
                float g = 0.5f * (1f - Mathf.Cos(Mathf.Repeat(now, glowPeriod) / glowPeriod * 2f * Mathf.PI));
                var c = glow.color;
                c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, g);
                glow.color = c;
            }

            if (shadow != null && visual != null && shadow.activeSelf != visual.activeSelf)
                shadow.SetActive(visual.activeSelf);
        }
    }
}
