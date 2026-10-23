using UnityEngine;

namespace ResortEmpire.Infrastructure
{
    // Unlocks high refresh rate displays (90/120 Hz) on phones and tablets.
    // Runs automatically before the first scene loads, no GameObject needed.
    public static class HighFrameRate
    {
        public const int TargetFps = 120;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (!Application.isMobilePlatform)
                return;

            // Mobile ignores vSync, but a non-zero value would override targetFrameRate elsewhere.
            QualitySettings.vSyncCount = 0;
            int displayMax = MaxRefreshRate();
            Application.targetFrameRate = displayMax > 0 ? Mathf.Min(TargetFps, displayMax) : TargetFps;
        }

        private static int MaxRefreshRate()
        {
            double best = Screen.currentResolution.refreshRateRatio.value;
            foreach (var resolution in Screen.resolutions)
                if (resolution.refreshRateRatio.value > best)
                    best = resolution.refreshRateRatio.value;
            return Mathf.RoundToInt((float)best);
        }
    }
}
