using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Failure physicality (REDESIGN.md §6): wrong answers crack the tile and
    /// spray red shards — "I broke something", not "oops". Complements (never
    /// replaces) FeedbackManager's shake + red flash on the same event.
    /// </summary>
    public class WrongAnswerFX : MonoBehaviour
    {
        [SerializeField] private Image crack;            // overlay child of the arrow tile
        [SerializeField] private RectTransform arrow;
        [SerializeField] private ParticleSystem shards;

        [Header("Tuning")]
        [SerializeField] private float crackHold = 0.1f;
        [SerializeField] private float crackFade = 0.15f;
        [SerializeField] private float dropPixels = 20f;
        [SerializeField] private int shardCount = 10;

        private Sequence _crackSeq;
        private Tween _dropTween;

        private void OnEnable()  => GameEvents.OnAnswerResolved += HandleAnswer;

        private void OnDisable()
        {
            GameEvents.OnAnswerResolved -= HandleAnswer;
            _crackSeq?.Kill();
            _dropTween?.Kill();
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (correct) return;

            if (crack != null)
            {
                _crackSeq?.Kill();
                // Random cardinal rotation so consecutive cracks read differently.
                crack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
                var c = crack.color; c.a = 1f; crack.color = c;
                _crackSeq = DOTween.Sequence().SetUpdate(true)
                    .AppendInterval(crackHold)
                    .Append(crack.DOFade(0f, crackFade));
            }

            if (arrow != null)
            {
                _dropTween?.Kill(true);
                _dropTween = arrow.DOPunchAnchorPos(Vector2.down * dropPixels, 0.2f, vibrato: 3, elasticity: 0.4f)
                    .SetUpdate(true);
            }

            if (shards != null)
                shards.Emit(shardCount);
        }

        private void OnDestroy()
        {
            _crackSeq?.Kill();
            _dropTween?.Kill();
        }
    }
}
