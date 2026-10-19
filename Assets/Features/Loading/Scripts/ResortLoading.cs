using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Presentation
{
    public sealed class ResortLoading : MonoBehaviour
    {
        public string TargetScene = "ResortUIDemo";
        public RectTransform Fill;
        [Min(3f)] public float MinimumDuration = 3f;

        private IEnumerator Start()
        {
            if (!Fill || !Application.CanStreamedLevelBeLoaded(TargetScene))
            {
                Debug.LogError("Loading: skontroluj Fill a cieľovú scénu v Build Settings.", this);
                yield break;
            }

            Fill.gameObject.SetActive(true);
            Fill.anchorMax = new Vector2(0f, 1f);
            float started = Time.realtimeSinceStartup;
            var loading = SceneManager.LoadSceneAsync(TargetScene);
            loading.allowSceneActivation = false;
            float duration = Mathf.Max(3f, MinimumDuration);

            while (Time.realtimeSinceStartup - started < duration || loading.progress < .9f)
            {
                float elapsed = Mathf.Clamp01((Time.realtimeSinceStartup - started) / duration);
                float progress = Mathf.Min(elapsed, loading.progress / .9f);
                Fill.anchorMax = new Vector2(progress, 1f);
                yield return null;
            }

            Fill.anchorMax = Vector2.one;
            yield return null;
            loading.allowSceneActivation = true;
        }
    }
}
