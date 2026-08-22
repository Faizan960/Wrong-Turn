using System.Collections.Generic;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.UI;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Maps GameState -> UIScreen. Screens are children of the UI canvas and
    /// register automatically on Awake. Pause is treated as an overlay on top
    /// of the gameplay HUD rather than a screen swap.
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        [SerializeField] private UIScreen[] screens;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private GameObject tutorialOverlay;   // Phase 7 — rides on the gameplay HUD

        private readonly Dictionary<GameState, UIScreen> _byState = new Dictionary<GameState, UIScreen>();
        private UIScreen _active;

        protected override void OnSingletonAwake()
        {
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                _byState[screen.HandledState] = screen;
                screen.gameObject.SetActive(false);
            }
            if (pauseOverlay != null) pauseOverlay.SetActive(false);
            if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
        }

        private void OnEnable()  => GameEvents.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameState from, GameState to)
        {
            // Pause: keep the HUD visible underneath, just toggle the overlay.
            if (to == GameState.Paused)
            {
                if (pauseOverlay != null) pauseOverlay.SetActive(true);
                return;
            }
            if (from == GameState.Paused && pauseOverlay != null)
                pauseOverlay.SetActive(false);

            if (from == GameState.Paused && to == GameState.Playing)
                return; // resuming — HUD is already up

            // Tutorial: gameplay HUD underneath (arrow, ring, hearts) with the
            // instruction overlay on top — same layering idea as pause.
            if (to == GameState.Tutorial)
            {
                if (tutorialOverlay != null) tutorialOverlay.SetActive(true);
                if (_active != null) _active.Hide();
                _active = _byState.TryGetValue(GameState.Playing, out var hud) ? hud : null;
                if (_active != null) _active.Show();
                return;
            }
            if (from == GameState.Tutorial)
            {
                if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
                if (to == GameState.Playing)
                    return; // tutorial ends into the first run — HUD is already up
            }

            if (_active != null) _active.Hide();
            _active = _byState.TryGetValue(to, out var screen) ? screen : null;
            if (_active != null) _active.Show();
        }
    }
}
