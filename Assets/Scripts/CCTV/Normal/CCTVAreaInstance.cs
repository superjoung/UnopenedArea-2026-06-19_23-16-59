using System.Collections.Generic;
using UnityEngine;

public class CCTVAreaInstance : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform propRoot;
    [SerializeField] private Transform anomalyRoot;
    [SerializeField] private Transform audioRoot;

    [Header("Main Room Variant Only")]
    [Tooltip("메인룸 계열 CCTV 프리팹에서만 Power/Calendar 연출을 사용합니다. 일반 관찰 구역에서는 끕니다.")]
    [SerializeField] private bool useMainRoomPowerAndCalendarVisuals;

    [Header("Power Visuals")]
    [Tooltip("비워두면 BackGround / DarkBackGround / Controll 이름으로 자동 연결합니다.")]
    [SerializeField] private GameObject normalBackground;
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private SpriteRenderer controlSignRenderer;
    [SerializeField, Range(0f, 1f)] private float controlSignBlackoutBrightness = 0.45f;

    [Header("Calendar Visuals")]
    [Tooltip("0=Day 1, 1=Day 2, 2=Day 3. 비워두면 Callender1~3을 자동 연결합니다.")]
    [SerializeField] private GameObject[] calendarDaySprites = new GameObject[3];

    [Header("Editor Measurement")]
    [Tooltip("AreaDefinition이 아직 연결되지 않은 프리팹 편집 상태에서 사용할 PPU입니다.")]
    [SerializeField, Min(1f)] private float editorMeasurementPixelsPerUnit = 100f;

    private readonly Dictionary<string, CCTVSceneObject> objectsById = new Dictionary<string, CCTVSceneObject>();
    private readonly Dictionary<string, CCTVObjectBaselineState> baselineByObjectId = new Dictionary<string, CCTVObjectBaselineState>();
    private Day1FlowController day1FlowController;
    private DayRuntimeController dayRuntimeController;
    private Color controlSignBaseColor;
    private bool controlSignColorCached;

    public CCTVAreaDefinition Definition { get; private set; }
    public Vector3 WorldOrigin { get; private set; }
    public Transform PropRoot => propRoot;
    public Transform AnomalyRoot => anomalyRoot;
    public Transform AudioRoot => audioRoot;

    private void Awake()
    {
        if (!useMainRoomPowerAndCalendarVisuals)
            return;

        ResolvePowerVisualReferences();
        ApplyPowerVisual(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
        RefreshCalendar();
    }

    private void OnEnable()
    {
        if (!useMainRoomPowerAndCalendarVisuals)
            return;

        ResolvePowerVisualReferences();
        SubscribeFlow();
        ApplyPowerVisual(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
        RefreshCalendar();
    }

    private void OnDisable()
    {
        if (useMainRoomPowerAndCalendarVisuals && day1FlowController != null)
            day1FlowController.StateChanged -= ApplyPowerVisual;
    }

    public void Initialize(CCTVAreaDefinition definition)
    {
        Initialize(definition, transform.position);
    }

    public void Initialize(CCTVAreaDefinition definition, Vector3 worldOrigin)
    {
        Definition = definition;
        WorldOrigin = worldOrigin;
        EnsureRoots();
        RebuildRegistry();
        CaptureBaseline();
    }

    public float GetViewWorldMinX()
    {
        if (Definition == null)
            return WorldOrigin.x;

        return WorldOrigin.x + Definition.ViewWorldMinX;
    }

    public float GetViewWorldMaxX()
    {
        if (Definition == null)
            return WorldOrigin.x;

        return WorldOrigin.x + Definition.ViewWorldMaxX;
    }

    public float GetViewWorldCenterY()
    {
        if (Definition == null)
            return WorldOrigin.y;

        return WorldOrigin.y + Definition.AreaCenter.y;
    }

    public bool TryGetObject(string objectId, out CCTVSceneObject sceneObject)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            sceneObject = null;
            return false;
        }

        return objectsById.TryGetValue(objectId, out sceneObject);
    }

    public IReadOnlyCollection<CCTVSceneObject> GetAllObjects()
    {
        return objectsById.Values;
    }

    public void CaptureBaseline()
    {
        baselineByObjectId.Clear();

        foreach (var pair in objectsById)
        {
            CCTVSceneObject sceneObject = pair.Value;
            if (sceneObject == null || !sceneObject.IncludeInBaseline)
                continue;

            baselineByObjectId[pair.Key] = new CCTVObjectBaselineState(sceneObject);
        }
    }

    public void RestoreBaseline()
    {
        foreach (var pair in baselineByObjectId)
        {
            if (!objectsById.TryGetValue(pair.Key, out CCTVSceneObject sceneObject) || sceneObject == null)
                continue;

            pair.Value.Restore(sceneObject);
        }
    }

    private void RebuildRegistry()
    {
        objectsById.Clear();
        CCTVSceneObject[] sceneObjects = GetComponentsInChildren<CCTVSceneObject>(true);

        foreach (CCTVSceneObject sceneObject in sceneObjects)
        {
            if (sceneObject == null || string.IsNullOrWhiteSpace(sceneObject.ObjectId))
            {
                Debug.LogWarning($"[CCTVAreaInstance] Object without id skipped. area={Definition?.AreaId}, object={sceneObject?.name}");
                continue;
            }

            if (objectsById.ContainsKey(sceneObject.ObjectId))
            {
                Debug.LogWarning($"[CCTVAreaInstance] Duplicate objectId skipped. area={Definition?.AreaId}, objectId={sceneObject.ObjectId}");
                continue;
            }

            objectsById.Add(sceneObject.ObjectId, sceneObject);
        }
    }

    private void EnsureRoots()
    {
        propRoot = EnsureRoot(propRoot, "PropRoot");
        anomalyRoot = EnsureRoot(anomalyRoot, "AnomalyRoot");
        audioRoot = EnsureRoot(audioRoot, "AudioRoot");
    }

    private Transform EnsureRoot(Transform root, string rootName)
    {
        if (root != null)
            return root;

        Transform found = transform.Find(rootName);
        if (found != null)
            return found;

        var go = new GameObject(rootName);
        found = go.transform;
        found.SetParent(transform, false);
        return found;
    }

    private void ResolvePowerVisualReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();

        if (normalBackground == null)
            normalBackground = FindChildObject("BackGround");
        if (darkBackground == null)
            darkBackground = FindChildObject("DarkBackGround");
        if (controlSignRenderer == null)
        {
            GameObject controlSign = FindChildObject("Controll");
            if (controlSign != null)
                controlSignRenderer = controlSign.GetComponent<SpriteRenderer>();
        }

        if (controlSignRenderer != null && !controlSignColorCached)
        {
            controlSignBaseColor = controlSignRenderer.color;
            controlSignColorCached = true;
        }

        for (int i = 0; i < calendarDaySprites.Length; i++)
        {
            if (calendarDaySprites[i] == null)
                calendarDaySprites[i] = FindChildObject($"Callender{i + 1}");
        }
    }

    private void SubscribeFlow()
    {
        if (day1FlowController == null)
            return;

        day1FlowController.StateChanged -= ApplyPowerVisual;
        day1FlowController.StateChanged += ApplyPowerVisual;
    }

    private void ApplyPowerVisual(Day1FlowState state)
    {
        bool isBlackout = state == Day1FlowState.EmergencyDispatch ||
                          (state == Day1FlowState.Monitoring && day1FlowController != null &&
                           day1FlowController.IsEmergencyPresentationLocked);

        SetActive(normalBackground, !isBlackout);
        SetActive(darkBackground, isBlackout);

        if (controlSignRenderer != null && controlSignColorCached)
        {
            Color color = controlSignBaseColor;
            if (isBlackout)
            {
                color.r *= controlSignBlackoutBrightness;
                color.g *= controlSignBlackoutBrightness;
                color.b *= controlSignBlackoutBrightness;
            }

            controlSignRenderer.color = color;
        }
    }

    private void RefreshCalendar()
    {
        int day = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : day1FlowController != null ? day1FlowController.DayNumber : 1;

        for (int i = 0; i < calendarDaySprites.Length; i++)
            SetActive(calendarDaySprites[i], i == day - 1);
    }

    private GameObject FindChildObject(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    [ContextMenu("Measure Background Bounds For Area Definition")]
    private void MeasureBackgroundBoundsForAreaDefinition()
    {
        bool hasBounds = false;
        Bounds combinedBounds = new Bounds();

        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Bg"))
                continue;

            foreach (SpriteRenderer renderer in child.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning($"[CCTVAreaInstance] No background SpriteRenderer found. Add a root whose name starts with 'Bg'. area={name}", this);
            return;
        }

        float pixelsPerUnit = Definition != null ? Definition.PixelsPerUnit : editorMeasurementPixelsPerUnit;
        int widthPixels = Mathf.CeilToInt(combinedBounds.size.x * pixelsPerUnit);
        int heightPixels = Mathf.CeilToInt(combinedBounds.size.y * pixelsPerUnit);
        Debug.Log(
            $"[CCTVAreaInstance] AreaDefinition recommendation: area={name}, " +
            $"world bounds=({combinedBounds.min.x:F2}~{combinedBounds.max.x:F2}, {combinedBounds.min.y:F2}~{combinedBounds.max.y:F2}), " +
            $"world size=({combinedBounds.size.x:F2}, {combinedBounds.size.y:F2}), " +
            $"Image Size Pixels=({widthPixels}, {heightPixels}) at PPU={pixelsPerUnit:F0}.", this);
    }
}
