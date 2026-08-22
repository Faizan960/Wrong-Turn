using UnityEngine;
using UnityEngine.UI;

namespace WrongDirection.Presentation
{
    /// <summary>Presentation-only round caps for the radial timer fill.</summary>
    public class TimerRingCaps : MonoBehaviour
    {
        [SerializeField] private Image ring;
        [SerializeField] private Image startCap;
        [SerializeField] private Image endCap;
        [SerializeField] private float radius = 342f;

        private void LateUpdate()
        {
            if (ring == null || startCap == null || endCap == null) return;

            float fill = ring.fillAmount;
            bool visible = fill > 0.01f && fill < 0.985f;
            startCap.gameObject.SetActive(visible);
            endCap.gameObject.SetActive(visible);
            if (!visible) return;

            Color color = ring.color;
            startCap.color = color;
            endCap.color = color;

            startCap.rectTransform.anchoredPosition = new Vector2(0f, radius);

            float angle = (90f - 360f * fill) * Mathf.Deg2Rad;
            endCap.rectTransform.anchoredPosition = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius);
        }
    }
}
