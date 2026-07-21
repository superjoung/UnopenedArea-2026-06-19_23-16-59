using UnityEngine;
using UnityEngine.UI;

public class CCTVScreenRigBootstrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private CCTVVisualProfile profile;

    [Header("Options")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private string canvasName = "Canvas_CCTV";
    [SerializeField] private string rawImageName = "RawImage_CCTVScreen";

    private CCTVScreenEffectController effectController;

    private void Start()
    {
        if (buildOnStart)
            BuildRig();
    }

    [ContextMenu("Build CCTV Screen Rig")]
    public void BuildRig()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        Canvas canvas = FindOrCreateCanvas();
        RawImage rawImage = FindOrCreateRawImage(canvas.transform);
        effectController = rawImage.GetComponent<CCTVScreenEffectController>();
        if (effectController == null)
            effectController = rawImage.gameObject.AddComponent<CCTVScreenEffectController>();

        effectController.Configure(worldCamera, rawImage, profile);
    }

    private Canvas FindOrCreateCanvas()
    {
        GameObject existing = GameObject.Find(canvasName);
        if (existing != null && existing.TryGetComponent(out Canvas foundCanvas))
            return foundCanvas;

        var canvasObject = new GameObject(canvasName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private RawImage FindOrCreateRawImage(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find(rawImageName);
        RawImage rawImage;

        if (existing != null && existing.TryGetComponent(out rawImage))
            return rawImage;

        var imageObject = new GameObject(rawImageName);
        imageObject.transform.SetParent(canvasTransform, false);
        rawImage = imageObject.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        RectTransform rect = rawImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rawImage;
    }
}
