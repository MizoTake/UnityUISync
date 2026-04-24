using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    public sealed class CanvasUiSyncPerformanceOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasUiSync primary;
        [SerializeField] private CanvasUiSync secondary;
        [SerializeField] private Text statusText;
        [SerializeField] private float refreshIntervalSeconds = 0.5f;
        private int frameCount;
        private float elapsedSeconds;
        private float nextRefreshTime;

        public void Configure(CanvasUiSync primaryTarget, CanvasUiSync secondaryTarget, Text statusTextTarget)
        {
            primary = primaryTarget;
            secondary = secondaryTarget;
            statusText = statusTextTarget;
            frameCount = 0;
            elapsedSeconds = 0f;
            nextRefreshTime = 0f;
            RefreshNow();
        }

        private void Awake()
        {
            RefreshNow();
        }

        private void Update()
        {
            frameCount++;
            elapsedSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            RefreshNow();
        }

        private void RefreshNow()
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshIntervalSeconds);
            if (statusText == null)
            {
                frameCount = 0;
                elapsedSeconds = 0f;
                return;
            }

            var fps = elapsedSeconds > 0f ? frameCount / elapsedSeconds : 0f;
            frameCount = 0;
            elapsedSeconds = 0f;
            statusText.text = "Perf Scene 128 controls / canvas | FPS " + fps.ToString("0.0") + " | " + Describe(primary) + " | " + Describe(secondary);
        }

        private static string Describe(CanvasUiSync target)
        {
            return target == null ? "missing" : target.name + " bindings=" + target.bindings.Count + " continuous=" + target.continuousBindings.Count + " polled=" + target.polledBindings.Count + " rescan=" + target.currentHierarchyRescanIntervalSeconds.ToString("0.00") + "s";
        }
    }
}
