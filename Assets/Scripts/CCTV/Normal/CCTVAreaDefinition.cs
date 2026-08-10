using UnityEngine;

[CreateAssetMenu(fileName = "CCTVAreaDefinition", menuName = "Unrecorded Area/CCTV Area Definition")]
public class CCTVAreaDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private AreaId areaId = AreaId.None;
    [SerializeField] private string displayName;

    [Header("Visual")]
    [SerializeField] private GameObject areaPrefab;
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("CCTV 화면에서만 적용되는 밝기 배율입니다. 1이면 공용 프로필 밝기를 그대로 사용합니다.")]
    [SerializeField, Range(0.5f, 2f)] private float cctvBrightnessMultiplier = 1f;

    [Header("Size")]
    [SerializeField] private Vector2Int imageSizePixels = new Vector2Int(3100, 500);
    [SerializeField] private float pixelsPerUnit = 100f;

    [Header("Camera")]
    [SerializeField] private Vector2 areaCenter = Vector2.zero;
    [SerializeField] private float orthographicSize = 2.5f;
    [SerializeField, Range(0f, 1f)] private float startNormalizedX = 0.5f;

    [Header("CCTV View Bounds")]
    [SerializeField] private bool useCustomViewBounds;
    [SerializeField] private float viewMinX;
    [SerializeField] private float viewMaxX;

    [Header("Baseline")]
    [SerializeField] private string baselineProfileId;

    public AreaId AreaId => areaId;
    public string DisplayName => displayName;
    public GameObject AreaPrefab => areaPrefab;
    public Sprite BackgroundSprite => backgroundSprite;
    public float CctvBrightnessMultiplier => Mathf.Clamp(cctvBrightnessMultiplier, 0.5f, 2f);
    public Vector2Int ImageSizePixels => imageSizePixels;
    public float PixelsPerUnit => Mathf.Max(1f, pixelsPerUnit);
    public Vector2 AreaCenter => areaCenter;
    public float OrthographicSize => Mathf.Max(0.01f, orthographicSize);
    public float StartNormalizedX => startNormalizedX;
    public bool UseCustomViewBounds => useCustomViewBounds;
    public float ViewMinX => viewMinX;
    public float ViewMaxX => viewMaxX;
    public string BaselineProfileId => baselineProfileId;

    public Vector2 WorldSize => new Vector2(
        imageSizePixels.x / PixelsPerUnit,
        imageSizePixels.y / PixelsPerUnit
    );

    public float DefaultWorldMinX => areaCenter.x - WorldSize.x * 0.5f;
    public float DefaultWorldMaxX => areaCenter.x + WorldSize.x * 0.5f;

    public float ViewWorldMinX => useCustomViewBounds ? Mathf.Min(viewMinX, viewMaxX) : DefaultWorldMinX;
    public float ViewWorldMaxX => useCustomViewBounds ? Mathf.Max(viewMinX, viewMaxX) : DefaultWorldMaxX;
}
