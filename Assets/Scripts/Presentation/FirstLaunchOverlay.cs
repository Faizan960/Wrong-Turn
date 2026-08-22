using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Premium first-launch onboarding sequence (Phase 7.5): pauses on the
    /// first menu, fades in a "WELCOME" text, then auto-opens the player
    /// guide (RulebookOverlay). Fires exactly once — firstLaunchCompleted
    /// in PlayerData prevents re-triggering. Same overlay pattern as
    /// SettingsOverlay / RulebookOverlay (CanvasGroup, no GameState change).
    /// Uses DOTween — no new animation system.
    /// </summary>
    public class FirstLaunchOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private RulebookOverlay rulebook;

        [Header("Timing")]
        [SerializeField] private float introDelay = 2f;
        [SerializeField] private float welcomeFadeDuration = 0.5f;
        [SerializeField] private float welcomeHoldDuration = 1f;

        private bool _shown;
        private Tween _fade;
        private Coroutine _sequence;

        private void Awake()
        {
            if (panel != null)
            {
                panel.alpha = 0f;
                panel.interactable = false;
                panel.blocksRaycasts = false;
            }
        }

        private void OnEnable()  => GameEvents.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to != GameState.Menu || _shown) return;
            _shown = true;
            if (!SaveManager.Exists || SaveManager.Instance.Data.firstLaunchCompleted) return;
            _sequence = StartCoroutine(FirstLaunchSequence());
        }

        private IEnumerator FirstLaunchSequence()
        {
            // Phase 1: let the menu settle — title, arrow, particles are visible
            yield return new WaitForSecondsRealtime(introDelay);

            // Phase 2: fade in "WELCOME" over the menu
            if (panel != null)
            {
                panel.blocksRaycasts = true;
                panel.interactable = true;
            }
            if (welcomeText != null)
            {
                welcomeText.alpha = 0f;
                _fade = welcomeText.DOFade(1f, welcomeFadeDuration)
                    .SetEase(Ease.OutQuad).SetUpdate(true);
            }
            if (panel != null)
            {
                panel.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(welcomeHoldDuration);

            // Phase 3: dismiss welcome, open the player guide
            if (welcomeText != null)
            {
                _fade?.Kill();
                _fade = welcomeText.DOFade(0f, 0.25f)
                    .SetEase(Ease.InQuad).SetUpdate(true);
            }
            yield return new WaitForSecondsRealtime(0.3f);

            if (panel != null)
            {
                panel.alpha = 0f;
                panel.blocksRaycasts = false;
                panel.interactable = false;
            }

            // Open the rulebook (player guide) — auto mode. The rulebook
            // saves firstLaunchCompleted when the player closes it, so a
            // launch interrupted mid-guide shows the guide once more.
            if (rulebook != null)
            {
                rulebook.Open(auto: true);
            }
            else if (SaveManager.Exists)
            {
                SaveManager.Instance.Data.firstLaunchCompleted = true;
                SaveManager.Instance.Save();
            }
        }

        private void OnDestroy()
        {
            _fade?.Kill();
            if (_sequence != null) StopCoroutine(_sequence);
        }
    }
}
