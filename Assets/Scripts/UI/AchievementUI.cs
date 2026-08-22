using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.UI
{
    /// <summary>
    /// Animated "achievement unlocked" toast. Pure presentation: listens to
    /// OnAchievementUnlocked, queues popups so simultaneous unlocks play in
    /// sequence instead of overlapping. Scene-placed under the Canvas.
    /// </summary>
    public class AchievementUI : MonoBehaviour
    {
        [SerializeField] private RectTransform popup;        // hidden banner, anchored top
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private CanvasGroup popupGroup;

        [Header("Tuning")]
        [SerializeField] private float slideDistance = 220f;
        [SerializeField] private float showDuration = 1.6f;

        private readonly Queue<AchievementData> _queue = new Queue<AchievementData>();
        private bool _playing;
        private Vector2 _homePos;

        private void Awake()
        {
            _homePos = popup.anchoredPosition;
            popupGroup.alpha = 0f;
        }

        private void OnEnable()  => GameEvents.OnAchievementUnlocked += HandleUnlocked;
        private void OnDisable() => GameEvents.OnAchievementUnlocked -= HandleUnlocked;

        private void HandleUnlocked(string id, int coinBonus)
        {
            // Resolve the definition once; array is tiny and this is a rare event.
            var all = AchievementData.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].id != id) continue;
                _queue.Enqueue(all[i]);
                if (!_playing) PlayNext();
                return;
            }
        }

        private void PlayNext()
        {
            if (_queue.Count == 0) { _playing = false; return; }
            _playing = true;

            var achievement = _queue.Dequeue();
            titleText.text = achievement.title;
            descriptionText.text = achievement.description;

            popup.anchoredPosition = _homePos + Vector2.up * slideDistance;
            popupGroup.alpha = 0f;

            DOTween.Sequence()
                .Append(popup.DOAnchorPos(_homePos, 0.35f).SetEase(Ease.OutBack))
                .Join(popupGroup.DOFade(1f, 0.25f))
                .AppendInterval(showDuration)
                .Append(popupGroup.DOFade(0f, 0.3f))
                .SetUpdate(true)                 // popups keep animating while paused
                .OnComplete(PlayNext);
        }
    }
}
