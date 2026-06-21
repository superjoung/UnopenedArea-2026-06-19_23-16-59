using UnityEngine;

public class AdaptiveUIRoot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AspectRangeController aspectRangeCamera;
    [SerializeField] private RectTransform viewportRoot;
    [SerializeField] private RectTransform safeAreaRoot;

    [Header("Options")]
    [SerializeField] private bool useSafeArea = true;

    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastSafeArea;
    private Rect lastViewport;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        Rect viewport = aspectRangeCamera != null
            ? aspectRangeCamera.CurrentViewportNormalized
            : new Rect(0f, 0f, 1f, 1f);

        bool changed =
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            Screen.safeArea != lastSafeArea ||
            viewport != lastViewport;

        if (changed)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (aspectRangeCamera == null)
            aspectRangeCamera = Camera.main.GetComponent<AspectRangeController>();

        Rect viewport = aspectRangeCamera != null
            ? aspectRangeCamera.CurrentViewportNormalized
            : new Rect(0f, 0f, 1f, 1f);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        lastViewport = viewport;

        ApplyViewport(viewport);
        ApplySafeArea(viewport);
    }

    private void ApplyViewport(Rect viewport)
    {
        if (viewportRoot == null) return;

        viewportRoot.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
        viewportRoot.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
        viewportRoot.offsetMin = Vector2.zero;
        viewportRoot.offsetMax = Vector2.zero;
    }

    private void ApplySafeArea(Rect viewportNormalized)
    {
        if (safeAreaRoot == null || viewportRoot == null)
            return;

        if (!useSafeArea)
        {
            safeAreaRoot.anchorMin = Vector2.zero;
            safeAreaRoot.anchorMax = Vector2.one;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            return;
        }

        Rect viewportPixels = new Rect(
            viewportNormalized.x * Screen.width,
            viewportNormalized.y * Screen.height,
            viewportNormalized.width * Screen.width,
            viewportNormalized.height * Screen.height
        );

        Rect safe = Screen.safeArea;
        Rect intersection = Intersect(viewportPixels, safe);

        if (intersection.width <= 0f || intersection.height <= 0f)
        {
            intersection = viewportPixels;
        }

        Vector2 anchorMin = new Vector2(
            (intersection.xMin - viewportPixels.xMin) / viewportPixels.width,
            (intersection.yMin - viewportPixels.yMin) / viewportPixels.height
        );

        Vector2 anchorMax = new Vector2(
            (intersection.xMax - viewportPixels.xMin) / viewportPixels.width,
            (intersection.yMax - viewportPixels.yMin) / viewportPixels.height
        );

        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        safeAreaRoot.anchorMin = anchorMin;
        safeAreaRoot.anchorMax = anchorMax;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMax < xMin || yMax < yMin)
            return new Rect(0, 0, 0, 0);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}