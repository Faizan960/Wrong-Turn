using System.Collections.Generic;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Generic prefab pool. Get/Release instead of Instantiate/Destroy so
    /// gameplay-time effects (particles, popups, trails) never allocate or
    /// trigger GC. Pools are keyed by prefab; instances self-describe their
    /// origin via a marker component to make Release foolproof.
    /// </summary>
    public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>
    {
        private sealed class PooledMarker : MonoBehaviour
        {
            public GameObject prefabKey;
        }

        private readonly Dictionary<GameObject, Stack<GameObject>> _pools =
            new Dictionary<GameObject, Stack<GameObject>>();

        /// <summary>Pre-instantiate so first use never spikes.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var pool = GetPool(prefab);
            for (int i = 0; i < count; i++)
            {
                var go = CreateInstance(prefab);
                go.SetActive(false);
                pool.Push(go);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            var pool = GetPool(prefab);
            GameObject go = null;
            while (pool.Count > 0 && go == null)
                go = pool.Pop();                    // skip anything externally destroyed

            if (go == null) go = CreateInstance(prefab);

            var t = go.transform;
            t.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }

        public T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var go = Get(prefab != null ? prefab.gameObject : null, position, rotation);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>Return an instance to its pool. Destroys unknown objects.</summary>
        public void Release(GameObject instance)
        {
            if (instance == null) return;

            var marker = instance.GetComponent<PooledMarker>();
            if (marker == null || marker.prefabKey == null)
            {
                Destroy(instance);                  // not ours — fail safe
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            GetPool(marker.prefabKey).Push(instance);
        }

        /// <summary>Release after a delay (particle bursts, popups).</summary>
        public void Release(GameObject instance, float delay) =>
            StartCoroutine(ReleaseLater(instance, delay));

        private System.Collections.IEnumerator ReleaseLater(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            Release(instance);
        }

        private Stack<GameObject> GetPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<GameObject>(16);
                _pools[prefab] = pool;
            }
            return pool;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            go.AddComponent<PooledMarker>().prefabKey = prefab;
            return go;
        }
    }
}
