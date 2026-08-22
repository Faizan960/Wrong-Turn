using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    /// <summary>
    /// First-run tutorial overlay (Phase 7). Activated by UIManager on top of
    /// the gameplay HUD while GameManager runs the scripted Tutorial state;
    /// this component only renders — step text, an animated finger hint that
    /// swipes in the answer direction, and a retry line on mistakes. All flow
    /// control (spawns, retries, completion) lives in GameManager.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private RectTransform finger;      // hint glyph, hidden for wait rules
        [SerializeField] private Image fingerImage;
        [SerializeField] private float fingerTravel = 140f;

        [Header("Rule accents")]
        [SerializeField] private Color whiteRule = new Color32(0xFF, 0xFF, 0xFF, 0xFF);   // #FFFFFF white = opposite
        [SerializeField] private Color blueRule = new Color32(0x16, 0x8C, 0xFF, 0xFF);    // #168CFF electric blue = same
        [SerializeField] private Color redRule = new Color32(0xFF, 0x30, 0x45, 0xFF);     // #FF3045
        [SerializeField] private Color purpleRule = new Color32(0xFF, 0xD6, 0x00, 0xFF);  // #FFD600 yellow tap rule (field name kept: scene compat)
        [SerializeField] private Color emeraldRule = new Color32(0x00, 0xE6, 0x76, 0xFF); // #00E676

        private static readonly string[] Titles =
        {
            "WHITE = OPPOSITE",
            "BLUE = SAME",
            "RED = DON'T TOUCH",
            "YELLOW = TAP ONCE",
            "EMERALD SAVES YOUR LIFE",
            "GOOD LUCK."
        };

        private static readonly string[] Subtitles =
        {
            "the arrow points — you swipe the other way",
            "blue is honest — swipe where it points",
            "let the timer run out. that IS the answer",
            "one tap, anywhere — the arrow's direction doesn't matter",
            "touch nothing — surviving it restores a heart",
            "NOW THE GAME STARTS LYING."
        };

        private int _step;
        private Sequence _fingerSeq;
        private Tween _subtitleTween;
        private Vector2 _fingerHome;

        private void Awake()
        {
            if (finger != null) _fingerHome = finger.anchoredPosition;
        }

        private void OnEnable()
        {
            GameEvents.OnTutorialStepChanged += HandleStep;
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;

            // Step 0 fires before UIManager activates this overlay — catch up.
            _step = GameManager.Exists ? Mathf.Max(0, GameManager.Instance.TutorialStep) : 0;
            Render();
        }

        private void OnDisable()
        {
            GameEvents.OnTutorialStepChanged -= HandleStep;
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            KillTweens();
        }

        private void HandleStep(int step)
        {
            _step = step;
            Render();
        }

        private void Render()
        {
            int i = Mathf.Clamp(_step, 0, Titles.Length - 1);
            if (titleText != null)
            {
                titleText.text = Titles[i];
                titleText.color = AccentFor(i);
            }
            if (subtitleText != null)
            {
                _subtitleTween?.Kill();
                subtitleText.text = Subtitles[i];
                subtitleText.alpha = 1f;
            }
            StopFinger();
        }

        private Color AccentFor(int step)
        {
            switch (step)
            {
                case 1:  return blueRule;
                case 2:  return redRule;
                case 3:  return purpleRule;
                case 4:  return emeraldRule;
                default: return whiteRule;
            }
        }

        /// <summary>Point the finger hint at the correct answer for this arrow.</summary>
        private void HandleInstruction(InstructionData data)
        {
            if (!isActiveAndEnabled) return;

            switch (data.Color)
            {
                case ColorRule.White:
                    AnimateFinger(data.Displayed.Opposite());
                    break;
                case ColorRule.Blue:
                    AnimateFinger(data.Displayed);
                    break;
                case ColorRule.Purple:
                    AnimateTapHint(); // stationary pulse: tap, don't swipe
                    break;
                default: // Red / Recovery — hands off
                    StopFinger();
                    break;
            }
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            StopFinger();
            if (correct || subtitleText == null || _step >= Titles.Length - 1) return;

            _subtitleTween?.Kill();
            subtitleText.text = "TRY AGAIN — NOTHING LOST HERE";
            subtitleText.alpha = 0f;
            _subtitleTween = subtitleText.DOFade(1f, 0.15f).SetUpdate(true);
        }

        private void AnimateFinger(Direction answer)
        {
            if (finger == null) return;
            StopFinger();

            finger.gameObject.SetActive(true);
            Vector2 travel = VectorFor(answer) * fingerTravel;
            _fingerSeq = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
                .AppendCallback(() =>
                {
                    finger.anchoredPosition = _fingerHome;
                    if (fingerImage != null) fingerImage.color = Color.white;
                })
                .Append(finger.DOAnchorPos(_fingerHome + travel, 0.5f).SetEase(Ease.InOutQuad));
            if (fingerImage != null)
                _fingerSeq.Join(fingerImage.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            _fingerSeq.AppendInterval(0.25f);
        }

        /// <summary>Tap-rule (yellow) hint: the finger stays put and pulses like a tap.</summary>
        private void AnimateTapHint()
        {
            if (finger == null) return;
            StopFinger();

            finger.gameObject.SetActive(true);
            finger.anchoredPosition = _fingerHome;
            _fingerSeq = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
                .AppendCallback(() =>
                {
                    finger.localScale = Vector3.one;
                    if (fingerImage != null) fingerImage.color = Color.white;
                })
                .Append(finger.DOScale(0.6f, 0.12f).SetEase(Ease.OutQuad))
                .Append(finger.DOScale(1f, 0.18f).SetEase(Ease.OutBack));
            if (fingerImage != null)
                _fingerSeq.Join(fingerImage.DOFade(0.4f, 0.18f).SetEase(Ease.InQuad));
            _fingerSeq.AppendInterval(0.4f);
        }

        private static Vector2 VectorFor(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:    return Vector2.up;
                case Direction.Down:  return Vector2.down;
                case Direction.Left:  return Vector2.left;
                default:              return Vector2.right;
            }
        }

        private void StopFinger()
        {
            _fingerSeq?.Kill();
            _fingerSeq = null;
            if (finger != null)
            {
                finger.localScale = Vector3.one; // the tap hint pulses scale
                finger.gameObject.SetActive(false);
            }
        }

        private void KillTweens()
        {
            StopFinger();
            _subtitleTween?.Kill();
        }

        private void OnDestroy() => KillTweens();
    }
}
