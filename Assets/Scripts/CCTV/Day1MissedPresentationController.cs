using System.Collections;
using UnityEngine;

/// <summary>
/// Day 1의 별도 미보고 공포 연출(D1_NR01, D1_NR02)을 확률적으로 재생합니다.
/// 오보고/미보고 횟수와 무관하며, 각 연출은 한 게임 사이클에 한 번만 성공할 수 있습니다.
/// </summary>
public sealed class Day1MissedPresentationController : MonoBehaviour
{
    [System.Serializable]
    private sealed class MissedPresentation
    {
        public string eventId;
        public AreaId areaId;
        [Range(0f, 1f)] public float probability = 0.1f;
        [Min(0.05f)] public float duration = 0.5f;
        public Sprite sprite;

        [System.NonSerialized] public bool played;
        [System.NonSerialized] public bool triggerWasEligible;
    }

    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private FieldModeController fieldModeController;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private CCTVScreenEffectController screenEffectController;
    [SerializeField] private Camera cctvCamera;

    [Header("D1_NR01 - Edge Person")]
    [SerializeField] private MissedPresentation edgePerson = new MissedPresentation
    {
        eventId = "D1_NR01",
        areaId = AreaId.LabCorridor,
        probability = 0.1f,
        duration = 0.7f,
    };
    [SerializeField] private Vector2 edgeStartViewport = new Vector2(1.12f, 0.45f);
    [SerializeField] private Vector2 edgeEndViewport = new Vector2(0.82f, 0.45f);
    [SerializeField, Range(0.1f, 1.5f)] private float edgePersonViewportHeight = 0.65f;

    [Header("D1_NR02 - Treatment Face")]
    [SerializeField] private MissedPresentation treatmentFace = new MissedPresentation
    {
        eventId = "D1_NR02",
        areaId = AreaId.TreatmentRoom,
        probability = 0.1f,
        duration = 0.5f,
    };

    private DayRuntimeController subscribedRuntime;
    private Coroutine activeRoutine;
    private SpriteRenderer feedRenderer;
    private AreaId cycleInitialAreaId = AreaId.None;
    private bool changedChannelSinceCycleStart;

    private void Awake()
    {
        ResolveReferences();
        ResetPresentations();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = null;
        SetFeedActive(false);
    }

    private void Update()
    {
        bool canAttempt = CanPreparePresentation() && HasChangedChannelSinceCycleStart() &&
                          activeRoutine == null && areaView.CurrentArea != null;
        AreaId currentAreaId = areaView.CurrentArea != null ? areaView.CurrentArea.AreaId : AreaId.None;
        TryStart(edgePerson, currentAreaId == edgePerson.areaId, canAttempt, PlayEdgePerson);
        TryStart(treatmentFace, currentAreaId == treatmentFace.areaId, canAttempt, PlayTreatmentFace);
    }

    private void TryStart(MissedPresentation presentation, bool isInTriggerArea, bool canAttempt,
        System.Func<MissedPresentation, IEnumerator> routineFactory)
    {
        if (presentation == null || presentation.played)
            return;

        if (!isInTriggerArea)
        {
            presentation.triggerWasEligible = false;
            return;
        }

        if (!canAttempt || presentation.triggerWasEligible)
            return;

        presentation.triggerWasEligible = true;
        bool success = Random.value <= presentation.probability;
        Debug.Log($"[Day1MissedPresentationController] chance event={presentation.eventId}, probability={presentation.probability:0.00}, success={success}", this);
        if (success)
            activeRoutine = StartCoroutine(routineFactory(presentation));
    }

    private IEnumerator PlayEdgePerson(MissedPresentation presentation)
    {
        if (!PrepareFeedRenderer(presentation.sprite, false))
        {
            activeRoutine = null;
            yield break;
        }

        presentation.played = true;
        SetFeedActive(true);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, presentation.duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetFeedViewportPosition(Vector2.Lerp(edgeStartViewport, edgeEndViewport, t));
            yield return null;
        }

        SetFeedActive(false);
        activeRoutine = null;
    }

    private IEnumerator PlayTreatmentFace(MissedPresentation presentation)
    {
        if (!PrepareFeedRenderer(presentation.sprite, true))
        {
            activeRoutine = null;
            yield break;
        }

        presentation.played = true;
        SetFeedActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, presentation.duration));
        SetFeedActive(false);
        activeRoutine = null;
    }

    private bool PrepareFeedRenderer(Sprite sprite, bool fillViewport)
    {
        if (sprite == null || cctvCamera == null)
        {
            Debug.LogWarning("[Day1MissedPresentationController] 미보고 연출 Sprite 또는 CCTV Camera가 비어 있습니다.", this);
            return false;
        }

        if (feedRenderer == null)
        {
            GameObject feedObject = new GameObject("D1_MissedPresentation_CCTVFeed");
            feedRenderer = feedObject.AddComponent<SpriteRenderer>();
            feedRenderer.sortingOrder = 32767;
        }

        feedRenderer.sprite = sprite;
        Transform feedTransform = feedRenderer.transform;
        feedTransform.SetParent(cctvCamera.transform, false);
        feedTransform.localRotation = Quaternion.identity;

        float distance = Mathf.Max(cctvCamera.nearClipPlane + 0.1f, 1f);
        float viewHeight = cctvCamera.orthographic
            ? cctvCamera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cctvCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cctvCamera.aspect;
        Vector3 spriteSize = sprite.bounds.size;
        float scale = fillViewport
            ? Mathf.Max(viewWidth / Mathf.Max(0.0001f, spriteSize.x), viewHeight / Mathf.Max(0.0001f, spriteSize.y))
            : viewHeight * edgePersonViewportHeight / Mathf.Max(0.0001f, spriteSize.y);
        feedTransform.localScale = Vector3.one * scale;
        feedTransform.localPosition = new Vector3(0f, 0f, distance);
        return true;
    }

    private void SetFeedViewportPosition(Vector2 viewport)
    {
        if (feedRenderer == null || cctvCamera == null)
            return;

        float distance = Mathf.Max(cctvCamera.nearClipPlane + 0.1f, 1f);
        float viewHeight = cctvCamera.orthographic
            ? cctvCamera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cctvCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cctvCamera.aspect;
        feedRenderer.transform.localPosition = new Vector3(
            (viewport.x - 0.5f) * viewWidth,
            (viewport.y - 0.5f) * viewHeight,
            distance);
    }

    private bool CanPreparePresentation()
    {
        if (dayRuntimeController == null || areaView == null || cctvCamera == null)
            return false;
        if (dayRuntimeController.State != DayRuntimeState.Running)
            return false;
        if (fieldModeController != null && fieldModeController.IsFieldModeActive)
            return false;
        if (sceneController != null && !sceneController.CCTVInputEnabled)
            return false;
        if (GameManager.Instance != null && !GameManager.Instance.ReportInputEnabled)
            return false;
        if (screenEffectController != null && screenEffectController.IsNoisePlaying)
            return false;
        if (anomalyService != null && anomalyService.ActiveAnomalies.Count > 0)
            return false;
        return true;
    }

    private void HandleDayStarted()
    {
        ResetPresentations();
        ResetInitialChannelGuard();
        SetFeedActive(false);
    }

    private bool HasChangedChannelSinceCycleStart()
    {
        AreaId currentAreaId = areaView?.CurrentArea != null ? areaView.CurrentArea.AreaId : AreaId.None;
        if (currentAreaId == AreaId.None)
            return false;

        if (cycleInitialAreaId == AreaId.None)
        {
            cycleInitialAreaId = currentAreaId;
            return false;
        }

        if (currentAreaId != cycleInitialAreaId)
            changedChannelSinceCycleStart = true;
        return changedChannelSinceCycleStart;
    }

    private void ResetInitialChannelGuard()
    {
        cycleInitialAreaId = areaView?.CurrentArea != null ? areaView.CurrentArea.AreaId : AreaId.None;
        changedChannelSinceCycleStart = false;
    }

    private void ResetPresentations()
    {
        ResetPresentation(edgePerson);
        ResetPresentation(treatmentFace);
    }

    private static void ResetPresentation(MissedPresentation presentation)
    {
        if (presentation == null)
            return;
        presentation.played = false;
        presentation.triggerWasEligible = false;
    }

    private void Subscribe()
    {
        if (dayRuntimeController == null || subscribedRuntime == dayRuntimeController)
            return;
        Unsubscribe();
        subscribedRuntime = dayRuntimeController;
        subscribedRuntime.DayStarted += HandleDayStarted;
    }

    private void Unsubscribe()
    {
        if (subscribedRuntime == null)
            return;
        subscribedRuntime.DayStarted -= HandleDayStarted;
        subscribedRuntime = null;
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (areaView == null)
            areaView = FindFirstObjectByType<CCTVAreaView>();
        if (sceneController == null)
            sceneController = FindFirstObjectByType<CCTVTestSceneController>();
        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>(FindObjectsInactive.Include);
        if (anomalyService == null)
            anomalyService = FindFirstObjectByType<AnomalyService>();
        if (screenEffectController == null)
            screenEffectController = FindFirstObjectByType<CCTVScreenEffectController>(FindObjectsInactive.Include);
        if (cctvCamera == null && screenEffectController != null)
            cctvCamera = screenEffectController.WorldCamera;
        if (cctvCamera == null)
            cctvCamera = Camera.main;
    }

    private void SetFeedActive(bool active)
    {
        if (feedRenderer != null)
            feedRenderer.enabled = active;
    }

    private void OnDestroy()
    {
        if (feedRenderer == null)
            return;
        if (Application.isPlaying)
            Destroy(feedRenderer.gameObject);
        else
            DestroyImmediate(feedRenderer.gameObject);
    }
}
