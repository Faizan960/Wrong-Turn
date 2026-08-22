using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Every Button under the canvas gets a soft click (Phase 5 Task 5).
    /// Wires listeners once at Start through AudioManager's existing public
    /// PlayClick() — includes inactive screens, so game-over / pause / settings
    /// buttons are covered without any screen knowing about audio.
    /// </summary>
    public class ButtonSfx : MonoBehaviour
    {
        private void Start()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(PlayClick);
        }

        private static void PlayClick()
        {
            if (AudioManager.Exists) AudioManager.Instance.PlayClick();
        }
    }
}
