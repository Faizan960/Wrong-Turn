using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// Lightweight generic singleton for manager MonoBehaviours.
    /// Survives scene loads and guarantees a single instance.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static bool _isQuitting;

        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;
                return _instance;
            }
        }

        public static bool Exists => _instance != null && !_isQuitting;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;

            // Only root objects can be marked DontDestroyOnLoad.
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            OnSingletonAwake();
        }

        /// <summary>Override instead of Awake in subclasses.</summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit() => _isQuitting = true;

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
