using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRangeController : MonoBehaviour
{
    [Header("Supported aspect range")]
    [SerializeField] private float minAspect = 16f / 9f;
    [SerializeField] private float maxAspect = 20f / 9f;

    [Header("Bars")]
    [SerializeField] private Color barColor = Color.black;

    private Camera cam;
    private Rect currentViewport = new Rect(0f, 0f, 1f, 1f);
    private int lastWidth;
    private int lastHeight;

    public Rect CurrentViewportNormalized => currentViewport;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = barColor;

        Apply();
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            Apply();
        }
    }

    public void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float currentAspect = (float)Screen.width / Screen.height;

        if (currentAspect < minAspect)
        {
            // 너무 좁은 화면 -> 위아래 여백
            float height = currentAspect / minAspect;
            float y = (1f - height) * 0.5f;
            currentViewport = new Rect(0f, y, 1f, height);
        }
        else if (currentAspect > maxAspect)
        {
            // 너무 넓은 화면 -> 좌우 여백
            float width = maxAspect / currentAspect;
            float x = (1f - width) * 0.5f;
            currentViewport = new Rect(x, 0f, width, 1f);
        }
        else
        {
            // 지원 범위 안 -> 전체 사용
            currentViewport = new Rect(0f, 0f, 1f, 1f);
        }

        cam.rect = currentViewport;
    }
}