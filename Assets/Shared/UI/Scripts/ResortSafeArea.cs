using UnityEngine;

namespace ResortEmpire.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class ResortSafeArea : MonoBehaviour
    {
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea ||
                lastScreenSize != new Vector2Int(Screen.width, Screen.height))
                Apply();
        }

        private void Apply()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Rect safeArea = Screen.safeArea;
            var minimum = new Vector2(Mathf.Clamp01(safeArea.xMin / width),
                Mathf.Clamp01(safeArea.yMin / height));
            var maximum = new Vector2(Mathf.Clamp01(safeArea.xMax / width),
                Mathf.Clamp01(safeArea.yMax / height));
            if (maximum.x <= minimum.x || maximum.y <= minimum.y)
            {
                minimum = Vector2.zero;
                maximum = Vector2.one;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
