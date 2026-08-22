using UnityEngine;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Converts raw touch (or editor keyboard) input into gesture events.
    /// Supports two schemes, chosen in settings:
    ///   Swipe — drag in a direction anywhere on screen; a short stationary
    ///           touch is a directionless tap (the Purple rule's answer).
    ///   Tap   — screen split into 4 triangular zones (top/bottom/left/right);
    ///           every touch is directional, GameManager reinterprets it as a
    ///           plain tap while a Purple instruction is live.
    /// Only emits while the game state is Playing.
    /// </summary>
    public class InputManager : MonoSingleton<InputManager>
    {
        [Header("Swipe")]
        [Tooltip("Minimum swipe distance in inches (scaled by DPI and user sensitivity).")]
        [SerializeField] private float minSwipeInches = 0.15f;
        [Tooltip("Max time (s) between touch start and end to count as a swipe.")]
        [SerializeField] private float maxSwipeTime = 0.6f;

        [Header("Tap (Purple rule)")]
        [Tooltip("Max time (s) finger down for a stationary touch to count as a tap; longer is a hold and emits nothing.")]
        [SerializeField] private float maxTapTime = 0.3f;

        private Vector2 _touchStart;
        private float _touchStartTime;
        private bool _tracking;
        private bool _inputEnabled;

        private ControlScheme Scheme =>
            SaveManager.Exists ? SaveManager.Instance.Data.settings.controlScheme : ControlScheme.Swipe;

        private float MinSwipePixels
        {
            get
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
                float sensitivity = SaveManager.Exists
                    ? Mathf.Max(0.25f, SaveManager.Instance.Data.settings.swipeSensitivity)
                    : 1f;
                return minSwipeInches * dpi / sensitivity;
            }
        }

        private void OnEnable()  => GameEvents.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameState from, GameState to)
        {
            _inputEnabled = to == GameState.Playing || to == GameState.Tutorial;
            _tracking = false;
        }

        private void Update()
        {
            if (!_inputEnabled) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            HandleKeyboard();
#endif
            HandleTouch();
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)    || Input.GetKeyDown(KeyCode.W)) Emit(Direction.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow)  || Input.GetKeyDown(KeyCode.S)) Emit(Direction.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) Emit(Direction.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Emit(Direction.Right);
            if (Input.GetKeyDown(KeyCode.Space)) GameEvents.RaiseTapInput(); // Purple rule
        }
#endif

        private void HandleTouch()
        {
            // Mouse fallback lets the editor test both schemes without a device.
            if (Input.GetMouseButtonDown(0)) BeginTouch(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && _tracking) EndTouch(Input.mousePosition);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began) BeginTouch(t.position);
                else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && _tracking)
                    EndTouch(t.position);
            }
        }

        private void BeginTouch(Vector2 pos)
        {
            _touchStart = pos;
            _touchStartTime = Time.unscaledTime;
            _tracking = true;
        }

        private void EndTouch(Vector2 pos)
        {
            _tracking = false;

            if (Scheme == ControlScheme.Tap)
            {
                Emit(ZoneFromPosition(pos));
                return;
            }

            Vector2 delta = pos - _touchStart;
            float elapsed = Time.unscaledTime - _touchStartTime;

            if (delta.magnitude < MinSwipePixels)
            {
                // Stationary touch: a quick one is a tap (Purple rule answers
                // fire the moment the finger lifts); a long hold emits nothing.
                if (elapsed <= maxTapTime) GameEvents.RaiseTapInput();
                return;
            }

            if (elapsed > maxSwipeTime)
                return; // moved, but too slow — neither swipe nor tap

            Emit(Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0f ? Direction.Right : Direction.Left)
                : (delta.y > 0f ? Direction.Up : Direction.Down));
        }

        /// <summary>
        /// Diagonal split into 4 triangles so every pixel maps unambiguously:
        /// closer to top edge = Up, etc.
        /// </summary>
        private static Direction ZoneFromPosition(Vector2 pos)
        {
            // Normalize to [-1, 1] on both axes, centered on screen.
            float nx = pos.x / Screen.width * 2f - 1f;
            float ny = pos.y / Screen.height * 2f - 1f;

            return Mathf.Abs(nx) > Mathf.Abs(ny)
                ? (nx > 0f ? Direction.Right : Direction.Left)
                : (ny > 0f ? Direction.Up : Direction.Down);
        }

        private static void Emit(Direction dir) => GameEvents.RaiseDirectionInput(dir);
    }
}
