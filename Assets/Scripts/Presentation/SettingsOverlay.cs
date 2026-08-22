using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Managers;
using WrongDirection.SaveSystem;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Settings screen (Phase 5 Task 4): black void + white typography, opened
    /// as a CanvasGroup overlay from the menu (no GameState change, like
    /// StatisticsUI). Rows are tap-to-cycle. Input/audio/vibration write the
    /// existing persisted SettingsData through SaveManager the sanctioned way;
    /// accessibility toggles live in AccessibilityPrefs (PlayerPrefs), so no
    /// manager or save schema is touched.
    /// </summary>
    public class SettingsOverlay : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        [Header("Rows (button + value label)")]
        [SerializeField] private Button inputButton;
        [SerializeField] private TMP_Text inputValue;
        [SerializeField] private Button difficultyButton;   // Phase 6: EASY / NORMAL preset
        [SerializeField] private TMP_Text difficultyValue;
        [SerializeField] private Button musicButton;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private Button sfxButton;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private TMP_Text vibrationValue;
        [SerializeField] private Button flashesButton;
        [SerializeField] private TMP_Text flashesValue;
        [SerializeField] private Button colorblindButton;
        [SerializeField] private TMP_Text colorblindValue;
        [SerializeField] private Button motionButton;
        [SerializeField] private TMP_Text motionValue;
        [SerializeField] private Button fpsButton;
        [SerializeField] private TMP_Text fpsValue;
        [SerializeField] private Button particlesButton;
        [SerializeField] private TMP_Text particlesValue;
        [SerializeField] private Button leftHandButton;
        [SerializeField] private TMP_Text leftHandValue;

        [Header("Transition (fade + slide)")]
        [SerializeField] private RectTransform content;   // slides up 40px on open
        [SerializeField] private float transitionSeconds = 0.22f;

        private Vector2 _contentHome;
        private Tween _fade, _slide;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (inputButton != null) inputButton.onClick.AddListener(ToggleInput);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(ToggleDifficulty);
            if (musicButton != null) musicButton.onClick.AddListener(() => CycleVolume(true));
            if (sfxButton != null) sfxButton.onClick.AddListener(() => CycleVolume(false));
            if (vibrationButton != null) vibrationButton.onClick.AddListener(ToggleVibration);
            if (flashesButton != null) flashesButton.onClick.AddListener(ToggleFlashes);
            if (colorblindButton != null) colorblindButton.onClick.AddListener(ToggleColorblind);
            if (motionButton != null) motionButton.onClick.AddListener(ToggleMotion);
            if (fpsButton != null) fpsButton.onClick.AddListener(ToggleFps);
            if (particlesButton != null) particlesButton.onClick.AddListener(ToggleParticles);
            if (leftHandButton != null) leftHandButton.onClick.AddListener(ToggleLeftHand);
            if (content != null) _contentHome = content.anchoredPosition;
            SetVisible(false, instant: true);
        }

        public void Open()
        {
            Refresh();
            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill();
            _slide?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;

            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                panel.alpha = visible ? 1f : 0f;
                if (content != null) content.anchoredPosition = _contentHome;
                return;
            }

            _fade = panel.DOFade(visible ? 1f : 0f, transitionSeconds)
                .SetEase(Ease.OutQuad).SetUpdate(true);
            if (content != null)
            {
                if (visible) content.anchoredPosition = _contentHome + Vector2.down * 40f;
                _slide = content.DOAnchorPos(
                        visible ? _contentHome : _contentHome + Vector2.down * 40f, transitionSeconds)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        private void ToggleInput()
        {
            var settings = Settings();
            if (settings == null) return;
            settings.controlScheme = settings.controlScheme == ControlScheme.Swipe
                ? ControlScheme.Tap
                : ControlScheme.Swipe;
            SaveManager.Instance.Save();
            Refresh();
        }

        /// <summary>Phase 6: EASY (5 lives, own board/stats, no achievements) ↔ NORMAL.</summary>
        private void ToggleDifficulty()
        {
            var settings = Settings();
            if (settings == null) return;
            settings.easyMode = !settings.easyMode;
            SaveManager.Instance.Save();
            Refresh();
        }

        private void CycleVolume(bool music)
        {
            var settings = Settings();
            if (settings == null) return;
            float current = music ? settings.musicVolume : settings.sfxVolume;
            float next = current >= 0.99f ? 0f : Mathf.Min(1f, current + 0.25f);
            if (music) settings.musicVolume = next; else settings.sfxVolume = next;
            SaveManager.Instance.Save();
            Refresh();
        }

        private void ToggleVibration()
        {
            var settings = Settings();
            if (settings == null) return;
            settings.vibration = !settings.vibration;
            SaveManager.Instance.Save();
            Refresh();
        }

        private void ToggleFlashes()
        {
            AccessibilityPrefs.ReduceFlashes = !AccessibilityPrefs.ReduceFlashes;
            Refresh();
        }

        private void ToggleColorblind()
        {
            AccessibilityPrefs.Colorblind = !AccessibilityPrefs.Colorblind;
            Refresh();
        }

        private void ToggleMotion()
        {
            AccessibilityPrefs.ReduceMotion = !AccessibilityPrefs.ReduceMotion;
            Refresh();
        }

        private void ToggleFps()
        {
            AccessibilityPrefs.HighFps = !AccessibilityPrefs.HighFps;
            FrameRateApplier.Apply();
            Refresh();
        }

        private void ToggleParticles()
        {
            AccessibilityPrefs.ReduceParticles = !AccessibilityPrefs.ReduceParticles;
            Refresh();
        }

        private void ToggleLeftHand()
        {
            var settings = Settings();
            if (settings == null) return;
            settings.leftHandedMode = !settings.leftHandedMode;
            SaveManager.Instance.Save();
            Refresh();
        }

        private static SettingsData Settings() =>
            SaveManager.Exists ? SaveManager.Instance.Data.settings : null;

        private void Refresh()
        {
            var settings = Settings();
            if (settings != null)
            {
                SetValue(inputValue, settings.controlScheme == ControlScheme.Swipe ? "SWIPE" : "TAP");
                SetValue(difficultyValue, settings.easyMode ? "EASY" : "NORMAL");
                SetValue(musicValue, $"{Mathf.RoundToInt(settings.musicVolume * 100f)}%");
                SetValue(sfxValue, $"{Mathf.RoundToInt(settings.sfxVolume * 100f)}%");
                SetValue(vibrationValue, settings.vibration ? "ON" : "OFF");
                SetValue(leftHandValue, settings.leftHandedMode ? "ON" : "OFF");
            }
            SetValue(flashesValue, AccessibilityPrefs.ReduceFlashes ? "ON" : "OFF");
            SetValue(colorblindValue, AccessibilityPrefs.Colorblind ? "ON" : "OFF");
            SetValue(motionValue, AccessibilityPrefs.ReduceMotion ? "ON" : "OFF");
            SetValue(fpsValue, AccessibilityPrefs.HighFps ? "120" : "60");
            SetValue(particlesValue, AccessibilityPrefs.ReduceParticles ? "OFF" : "ON");
        }

        private static void SetValue(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
