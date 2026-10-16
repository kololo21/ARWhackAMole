using UnityEngine;

namespace WhackAMole.Sensors
{
    // Only the displayed AR image changes. Tracking and game coordinates stay intact.
    public sealed class CameraDisplaySettings : MonoBehaviour
    {
        public static CameraDisplaySettings Instance { get; private set; }
        public bool FlipHorizontal { get; private set; }
        public bool FlipVertical { get; private set; }
        public bool IsFlipped => FlipHorizontal || FlipVertical;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearInstance() { Instance = null; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Configure(bool horizontal, bool vertical)
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing) return;
            FlipHorizontal = horizontal;
            FlipVertical = vertical;
        }

        // Labels are overlay UI: move their anchor to match the mirrored AR image,
        // while keeping the text itself readable.
        public static Vector3 ToDisplayPoint(Vector3 screen, Camera camera)
        {
            var settings = Instance;
            if (settings == null || camera == null) return screen;
            var rect = camera.pixelRect;
            if (settings.FlipHorizontal) screen.x = rect.xMin + rect.xMax - screen.x;
            if (settings.FlipVertical) screen.y = rect.yMin + rect.yMax - screen.y;
            return screen;
        }
    }
}
