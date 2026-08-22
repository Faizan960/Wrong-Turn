using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.UI
{
    /// <summary>
    /// Base for full-screen UI panels. UIManager toggles them based on the
    /// game state each screen declares itself responsible for.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public abstract GameState HandledState { get; }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            OnShow();
        }

        public virtual void Hide()
        {
            OnHide();
            gameObject.SetActive(false);
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}
