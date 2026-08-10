using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FoundAnomalyPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool closeWhenClickingOutside = true;
    [SerializeField] private MainRoomInteractionController mainRoomInteractionController;

    [Header("Scroll View")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField, Min(0f)] private float entrySpacing;

    [Header("Entry")]
    [SerializeField] private GameObject foundPrefab;
    [SerializeField] private string foundPrefabResourcePath = "Prefabs/UI/FoundPrefab";

    private readonly HashSet<AnomalyRuntime> recordedAnomalies = new HashSet<AnomalyRuntime>();
    private GameManager subscribedGameManager;
    private Coroutine scrollRoutine;
    private int nextNumber = 1;
    private bool scrollToBottomPending;
    private int openedFrame = -1;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        ResolveReferences();
        PrepareTemplate();
        ConfigureScrollView();
        Subscribe();

        if (hideOnStart && panelRoot != null)
            panelRoot.SetActive(false);

        SetMainRoomInputBlocked(IsOpen);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureScrollView();
        Subscribe();
        SetMainRoomInputBlocked(IsOpen);

        if (scrollToBottomPending)
            ScheduleScrollToBottom();
    }

    private void Update()
    {
        if (!closeWhenClickingOutside || !IsOpen || Time.frameCount == openedFrame)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        RectTransform panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
            return;

        Canvas parentCanvas = panelRoot.GetComponentInParent<Canvas>();
        Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, eventCamera))
            ClosePanel();
    }

    private void OnDestroy()
    {
        SetMainRoomInputBlocked(false);
        Unsubscribe();
    }

    public void OpenPanel()
    {
        ResolveReferences();
        ConfigureScrollView();

        if (panelRoot == null)
        {
            Debug.LogWarning("[FoundAnomalyPanelController] FoundPanel을 찾을 수 없습니다.", this);
            return;
        }

        SetMainRoomInputBlocked(true);
        panelRoot.SetActive(true);
        openedFrame = Time.frameCount;
        SoundManager.Instance?.PlayFoundAnomalyPanelOpenSfx();
        ScheduleScrollToBottom();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        openedFrame = -1;
        SetMainRoomInputBlocked(false);
    }

    public void TogglePanel()
    {
        if (IsOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    private void HandleCorrectReport(AnomalyRuntime runtime)
    {
        AnomalyDefinition definition = runtime != null ? runtime.Definition : null;
        if (definition == null || recordedAnomalies.Contains(runtime))
            return;

        ResolveReferences();
        if (contentRoot == null || foundPrefab == null)
        {
            Debug.LogWarning("[FoundAnomalyPanelController] Content 또는 FoundPrefab 할당이 필요합니다.", this);
            return;
        }

        recordedAnomalies.Add(runtime);
        GameObject entryObject = Instantiate(foundPrefab, contentRoot, false);
        entryObject.name = $"FoundEntry_{nextNumber:00}_{definition.AnomalyId}";
        entryObject.SetActive(true);

        FoundAnomalyEntryUI entry = entryObject.GetComponent<FoundAnomalyEntryUI>();
        if (entry == null)
            entry = entryObject.AddComponent<FoundAnomalyEntryUI>();

        entry.Bind(
            nextNumber,
            CCTVReportLabelProvider.GetAreaLabel(definition.AreaId),
            CCTVReportLabelProvider.GetTargetLabel(definition.ReportTargetId),
            CCTVReportLabelProvider.GetReportTypeLabel(definition.ReportType));

        nextNumber++;
        scrollToBottomPending = true;
        ScheduleScrollToBottom();
    }

    private void ConfigureScrollView()
    {
        if (scrollRect == null || contentRoot == null)
            return;

        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (scrollRect.horizontalScrollbar != null)
            scrollRect.horizontalScrollbar.gameObject.SetActive(false);

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, contentRoot.sizeDelta.y);

        VerticalLayoutGroup layoutGroup = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
            layoutGroup = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.spacing = entrySpacing;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
            sizeFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ScheduleScrollToBottom()
    {
        scrollToBottomPending = true;
        if (!isActiveAndEnabled || panelRoot == null || !panelRoot.activeInHierarchy || scrollRect == null)
            return;

        if (scrollRoutine != null)
            StopCoroutine(scrollRoutine);

        scrollRoutine = StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;

        if (panelRoot == null || !panelRoot.activeInHierarchy || scrollRect == null || contentRoot == null)
        {
            scrollRoutine = null;
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        scrollRect.verticalNormalizedPosition = 0f;
        scrollToBottomPending = false;
        scrollRoutine = null;
    }

    private void Subscribe()
    {
        GameManager sceneGameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (sceneGameManager == null || subscribedGameManager == sceneGameManager)
            return;

        Unsubscribe();
        subscribedGameManager = sceneGameManager;
        subscribedGameManager.DayCorrectReport += HandleCorrectReport;
    }

    private void Unsubscribe()
    {
        if (subscribedGameManager == null)
            return;

        subscribedGameManager.DayCorrectReport -= HandleCorrectReport;
        subscribedGameManager = null;
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
            panelRoot = FindChildByName(transform, "FoundPanel");

        if (scrollRect == null && panelRoot != null)
            scrollRect = panelRoot.GetComponentInChildren<ScrollRect>(true);

        if (contentRoot == null && scrollRect != null)
        {
            contentRoot = scrollRect.content;
            if (contentRoot == null)
                contentRoot = FindChildRectByName(scrollRect.transform, "Content");
        }

        if (foundPrefab == null && !string.IsNullOrWhiteSpace(foundPrefabResourcePath))
            foundPrefab = Resources.Load<GameObject>(foundPrefabResourcePath);

        if (foundPrefab == null && panelRoot != null)
            foundPrefab = FindChildByName(panelRoot.transform, "FoundPrefab");

        if (mainRoomInteractionController == null)
            mainRoomInteractionController = FindFirstObjectByType<MainRoomInteractionController>(FindObjectsInactive.Include);
    }

    private void PrepareTemplate()
    {
        if (panelRoot != null)
        {
            GameObject sceneTemplate = FindChildByName(panelRoot.transform, "FoundPrefab");
            if (sceneTemplate != null && sceneTemplate.scene.IsValid())
                sceneTemplate.SetActive(false);
        }

        if (foundPrefab != null && foundPrefab.scene.IsValid())
            foundPrefab.SetActive(false);
    }

    private void SetMainRoomInputBlocked(bool blocked)
    {
        if (mainRoomInteractionController == null)
            mainRoomInteractionController = FindFirstObjectByType<MainRoomInteractionController>(FindObjectsInactive.Include);

        mainRoomInteractionController?.SetReportPanelInputLocked(blocked);
    }

    private static GameObject FindChildByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName)
                return child.gameObject;
        }

        return null;
    }

    private static RectTransform FindChildRectByName(Transform root, string targetName)
    {
        GameObject target = FindChildByName(root, targetName);
        return target != null ? target.GetComponent<RectTransform>() : null;
    }
}
