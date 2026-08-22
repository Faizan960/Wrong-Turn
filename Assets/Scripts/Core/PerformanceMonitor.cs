using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace WrongDirection.Core
{
    /// <summary>
    /// Debug FPS/memory/GC overlay. Compiled into development builds and the
    /// editor only — the entire class body is stripped from release builds.
    /// Zero steady-state allocations: one reused StringBuilder, GUI label,
    /// half-second refresh.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private readonly StringBuilder _sb = new StringBuilder(128);
        private string _display = "";
        private bool _visible = true;

        private int _frames;
        private float _elapsed;
        private float _worstFrame;
        private int _lastGcCount;
        private GUIStyle _style;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;

            _frames++;
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            if (dt > _worstFrame) _worstFrame = dt;

            if (_elapsed < refreshInterval) return;

            float fps = _frames / _elapsed;
            float avgMs = _elapsed / _frames * 1000f;
            long memMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            int gc = System.GC.CollectionCount(0);

            _sb.Clear();
            _sb.Append("FPS ").Append((int)fps)
               .Append("  |  ").Append(avgMs.ToString("0.0")).Append("ms (worst ")
               .Append((_worstFrame * 1000f).ToString("0.0")).Append(")\n")
               .Append("MEM ").Append(memMb).Append("MB  |  GC#").Append(gc);
            if (gc != _lastGcCount) _sb.Append("  ⚠ GC SPIKE");
            _display = _sb.ToString();

            _lastGcCount = gc;
            _frames = 0;
            _elapsed = 0f;
            _worstFrame = 0f;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 50),
                    normal = { textColor = Color.green }
                };
            }
            GUI.Label(new Rect(10, 10, 500, 80), _display, _style);
        }
#endif
    }
}
