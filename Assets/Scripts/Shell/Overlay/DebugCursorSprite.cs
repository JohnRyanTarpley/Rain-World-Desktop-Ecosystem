using UnityEngine;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// Spike visuals for <see cref="OverlayWindow"/>: a dot that follows the cursor, plus a square
    /// target in the middle of the overlay that stands in for a creature. Over the target the overlay
    /// takes the mouse (the dot turns orange); anywhere else clicks fall through to the desktop
    /// (the dot is green).
    /// </summary>
    [RequireComponent(typeof(OverlayWindow))]
    public sealed class DebugCursorSprite : MonoBehaviour
    {
        [SerializeField] float cursorSizePx = 24f;
        [SerializeField] float targetSizePx = 160f;
        [SerializeField] Color clickThroughColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        [SerializeField] Color capturedColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        [SerializeField] Color targetColor = new Color(0.4f, 0.6f, 1f, 0.5f);

        OverlayWindow overlay;
        SpriteRenderer cursor;
        SpriteRenderer target;
        System.Func<Vector2, bool> hitTest;

        void Awake()
        {
            overlay = GetComponent<OverlayWindow>();
            target = MakeSprite("Debug Target", MakeTexture(false), targetColor, 0);
            cursor = MakeSprite("Debug Cursor", MakeTexture(true), clickThroughColor, 1);
            hitTest = OverTarget;
            overlay.HitTest = hitTest;
        }

        void OnDestroy()
        {
            if (overlay != null && overlay.HitTest == hitTest) overlay.HitTest = null;
        }

        bool OverTarget(Vector2 cursorPx)
        {
            var half = targetSizePx * 0.5f;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return Mathf.Abs(cursorPx.x - centre.x) <= half && Mathf.Abs(cursorPx.y - centre.y) <= half;
        }

        // LateUpdate so the dot uses the cursor and click-through state OverlayWindow read this frame.
        void LateUpdate()
        {
            var cam = overlay.Camera;
            var unitsPerPx = cam.orthographic ? 2f * cam.orthographicSize / Mathf.Max(1, Screen.height) : 0.01f;

            Place(target, cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), targetSizePx * unitsPerPx);
            Place(cursor, cam, overlay.CursorPosition, cursorSizePx * unitsPerPx);
            cursor.color = overlay.IsClickThrough ? clickThroughColor : capturedColor;
        }

        static void Place(SpriteRenderer sprite, Camera cam, Vector2 screenPx, float worldSize)
        {
            var depth = cam.nearClipPlane + 1f;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPx.x, screenPx.y, depth));
            sprite.transform.position = world;
            sprite.transform.localScale = new Vector3(worldSize, worldSize, 1f);
        }

        SpriteRenderer MakeSprite(string name, Texture2D texture, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // One world unit across, so localScale is the size in world units.
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Texture2D MakeTexture(bool circle)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var r = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - r;
                var dy = y + 0.5f - r;
                // A 1 px soft edge on the circle; the square is solid.
                var a = circle ? Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)) : 1f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
