using System.Collections;
using UnityEngine;

/// <summary>
/// Day 3의 보고 불가 미보고 공포(D3_NR01~03)를 담당합니다.
/// 일반 이상현상 정의/보고 판정과 분리하며, 미보고가 등록된 뒤 각 연출 조건에서 확률을 판정합니다.
/// </summary>
public sealed class Day3MissedPresentationController : MonoBehaviour
{
    [System.Serializable]
    private sealed class MissedPresentation
    {
        public string eventId;
        public AreaId areaId;
        public string effectObjectId;
        [Range(0f, 1f)] public float probability = 0.7f;
        [Min(0.05f)] public float duration = 0.5f;
        [Min(0f)] public float triggerDelay;
        [Tooltip("세로 방향으로 화면 밖을 허용할 여유입니다.")]
        [Range(0f, 0.5f)] public float viewportMargin = 0.05f;
        [Tooltip("화면 가로 중앙(0.5)에서 이 거리 안에 들어왔을 때 실행합니다. 0.2면 화면 중앙 40% 구간입니다.")]
        [Range(0.01f, 0.5f)] public float horizontalCenterTolerance = 0.2f;
        public bool lockCctvInput;
        public bool moveDuringPresentation;
        public Vector3 movementLocalOffset;
        [Min(0.01f)] public float frameDuration = 0.1f;
        public Sprite[] frames;
        public AudioClip sfx;

        [System.NonSerialized] public bool played;
        [System.NonSerialized] public int lastAttemptedMissedCount;
    }

    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVPanController panController;
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private FieldModeController fieldModeController;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private CCTVScreenEffectController screenEffectController;
    [SerializeField] private Camera cctvCamera;
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("D3_NR01 - Server Face")]
    [SerializeField] private MissedPresentation serverFace = new MissedPresentation
    {
        eventId = "D3_NR01",
        areaId = AreaId.ServerRoom,
        probability = 0.7f,
        duration = 0.5f,
        triggerDelay = 0.35f,
    };

    [Header("D3_NR02 - Ceiling Person")]
    [SerializeField] private MissedPresentation ceilingPerson = new MissedPresentation
    {
        eventId = "D3_NR02",
        areaId = AreaId.LabCorridor,
        effectObjectId = "OBJ_DORM_CEILING_PERSON_01",
        probability = 0.7f,
        duration = 0.8f,
        viewportMargin = 0.05f,
        horizontalCenterTolerance = 0.2f,
        lockCctvInput = true,
        moveDuringPresentation = true,
        movementLocalOffset = new Vector3(5f, 0f, 0f),
        frameDuration = 0.1f,
    };

    [Header("D3_NR03 - Reflection Person")]
    [SerializeField] private MissedPresentation reflectionPerson = new MissedPresentation
    {
        eventId = "D3_NR03",
        areaId = AreaId.TreatmentRoom,
        effectObjectId = "OBJ_TREAT_REFLECTION_01",
        probability = 0.7f,
        duration = 2f,
        viewportMargin = 0.05f,
        horizontalCenterTolerance = 0.2f,
        frameDuration = 0.1f,
    };

    [Tooltip("NR03가 발생하지 않은 평상시 반사면 이미지입니다. 비우면 프리팹 SpriteRenderer의 현재 이미지를 사용합니다.")]
    [SerializeField] private Sprite reflectionNormalSprite;
    [Tooltip("NR03 발생 중 잠시 표시할 비정상 반사 이미지입니다.")]
    [SerializeField] private Sprite reflectionAbnormalSprite;

    [Header("Debug")]
    [Tooltip("켜면 확률을 무시하고 조건을 만족한 연출을 항상 재생합니다.")]
    [SerializeField] private bool forceProbabilitySuccess;

    private DayRuntimeController subscribedRuntime;
    private Coroutine activeRoutine;
    private Coroutine pendingTimedRoutine;
    private SpriteRenderer serverFaceRenderer;

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
        StopActiveRoutines();
        SetServerFaceActive(false);
        RestoreAllEffectRoots();
    }

    private void Update()
    {
        if (!CanPreparePresentation() || activeRoutine != null || pendingTimedRoutine != null)
            return;

        int missedCount = dayRuntimeController.MissedAnomalyCount;
        if (missedCount <= 0 || areaView.CurrentArea == null)
            return;

        AreaId currentAreaId = areaView.CurrentArea.AreaId;
        if (currentAreaId == serverFace.areaId)
        {
            TryScheduleTimedPresentation(serverFace, missedCount);
            return;
        }

        if (currentAreaId == ceilingPerson.areaId)
        {
            TryStartViewportPresentation(ceilingPerson, missedCount);
            return;
        }

        if (currentAreaId == reflectionPerson.areaId)
            TryStartViewportPresentation(reflectionPerson, missedCount);
    }

    private void TryScheduleTimedPresentation(MissedPresentation presentation, int missedCount)
    {
        if (!CanAttempt(presentation, missedCount))
            return;

        presentation.lastAttemptedMissedCount = missedCount;
        pendingTimedRoutine = StartCoroutine(TryPlayTimedPresentation(presentation));
    }

    private IEnumerator TryPlayTimedPresentation(MissedPresentation presentation)
    {
        if (presentation.triggerDelay > 0f)
            yield return new WaitForSecondsRealtime(presentation.triggerDelay);

        pendingTimedRoutine = null;
        if (!CanPreparePresentation() || areaView.CurrentArea == null ||
            areaView.CurrentArea.AreaId != presentation.areaId || !Roll(presentation))
            yield break;

        activeRoutine = StartCoroutine(PlayServerFace(presentation));
    }

    private void TryStartViewportPresentation(MissedPresentation presentation, int missedCount)
    {
        if (!CanAttempt(presentation, missedCount) ||
            !TryGetCurrentEffectRoot(presentation, out GameObject effectRoot) ||
            !IsInsideCenterTriggerZone(
                effectRoot.transform,
                presentation.viewportMargin,
                presentation.horizontalCenterTolerance))
            return;

        presentation.lastAttemptedMissedCount = missedCount;
        if (!Roll(presentation))
            return;

        activeRoutine = presentation == reflectionPerson
            ? StartCoroutine(PlayReflectionSpriteSwap(presentation, effectRoot))
            : StartCoroutine(PlayViewportPresentation(presentation, effectRoot));
    }

    private IEnumerator PlayServerFace(MissedPresentation presentation)
    {
        presentation.played = true;
        PrepareServerFaceRenderer(presentation);
        if (serverFaceRenderer == null || serverFaceRenderer.sprite == null)
        {
            Debug.LogWarning($"[Day3MissedPresentationController] {presentation.eventId} sprite is missing.", this);
            activeRoutine = null;
            yield break;
        }

        PlaySfx(presentation.sfx);
        SetServerFaceActive(true);
        screenEffectController?.PlayTransitionNoise(presentation.duration);
        yield return new WaitForSecondsRealtime(presentation.duration);
        SetServerFaceActive(false);
        activeRoutine = null;
    }

    private IEnumerator PlayReflectionSpriteSwap(MissedPresentation presentation, GameObject effectRoot)
    {
        SpriteRenderer renderer = effectRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null || reflectionAbnormalSprite == null)
        {
            Debug.LogWarning($"[Day3MissedPresentationController] {presentation.eventId} abnormal sprite is missing.", this);
            activeRoutine = null;
            yield break;
        }

        Sprite normalSprite = reflectionNormalSprite != null ? reflectionNormalSprite : renderer.sprite;
        presentation.played = true;
        effectRoot.SetActive(true);
        renderer.sprite = reflectionAbnormalSprite;
        PlaySfx(presentation.sfx);
        yield return new WaitForSecondsRealtime(presentation.duration);

        if (renderer != null)
            renderer.sprite = normalSprite;
        activeRoutine = null;
    }

    private IEnumerator PlayViewportPresentation(MissedPresentation presentation, GameObject effectRoot)
    {
        presentation.played = true;
        bool previousCctvInput = sceneController == null || sceneController.CCTVInputEnabled;
        bool previousPanLock = panController != null && panController.InputLocked;

        if (presentation.lockCctvInput)
        {
            sceneController?.SetCCTVInputEnabled(false);
            panController?.SetInputLocked(true);
        }

        Transform effectTransform = effectRoot.transform;
        Vector3 startLocalPosition = effectTransform.localPosition;
        SpriteRenderer renderer = effectRoot.GetComponentInChildren<SpriteRenderer>(true);
        effectRoot.SetActive(true);
        PlaySfx(presentation.sfx);

        float elapsed = 0f;
        int lastFrame = -1;
        while (elapsed < presentation.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, presentation.duration));

            if (presentation.moveDuringPresentation)
                effectTransform.localPosition = Vector3.Lerp(startLocalPosition, startLocalPosition + presentation.movementLocalOffset, normalized);

            if (renderer != null && presentation.frames != null && presentation.frames.Length > 0)
            {
                int frame = Mathf.Min(
                    presentation.frames.Length - 1,
                    Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, presentation.frameDuration)) % presentation.frames.Length);
                if (frame != lastFrame && presentation.frames[frame] != null)
                {
                    renderer.sprite = presentation.frames[frame];
                    lastFrame = frame;
                }
            }

            yield return null;
        }

        effectTransform.localPosition = startLocalPosition;
        effectRoot.SetActive(false);

        if (presentation.lockCctvInput)
        {
            sceneController?.SetCCTVInputEnabled(previousCctvInput);
            panController?.SetInputLocked(previousPanLock);
        }

        activeRoutine = null;
    }

    private bool CanPreparePresentation()
    {
        if (dayRuntimeController == null || areaView == null || cctvCamera == null)
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

    private static bool CanAttempt(MissedPresentation presentation, int missedCount)
    {
        return presentation != null && !presentation.played &&
               presentation.lastAttemptedMissedCount < missedCount;
    }

    private bool Roll(MissedPresentation presentation)
    {
        bool success = forceProbabilitySuccess || Random.value <= presentation.probability;
        Debug.Log($"[Day3MissedPresentationController] chance event={presentation.eventId}, probability={presentation.probability:0.00}, success={success}", this);
        return success;
    }

    private bool IsInsideCenterTriggerZone(Transform target, float verticalMargin, float horizontalCenterTolerance)
    {
        if (target == null || cctvCamera == null)
            return false;

        Vector3 viewport = cctvCamera.WorldToViewportPoint(target.position);
        float minY = -Mathf.Max(0f, verticalMargin);
        float maxY = 1f + Mathf.Max(0f, verticalMargin);
        float tolerance = Mathf.Clamp(horizontalCenterTolerance, 0.01f, 0.5f);
        return viewport.z > 0f &&
               Mathf.Abs(viewport.x - 0.5f) <= tolerance &&
               viewport.y >= minY && viewport.y <= maxY;
    }

    private bool TryGetCurrentEffectRoot(MissedPresentation presentation, out GameObject effectRoot)
    {
        effectRoot = null;
        CCTVAreaInstance instance = areaView != null ? areaView.CurrentInstance : null;
        if (instance == null || string.IsNullOrWhiteSpace(presentation.effectObjectId) ||
            !instance.TryGetObject(presentation.effectObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
            return false;

        effectRoot = sceneObject.gameObject;
        return true;
    }

    private void PrepareServerFaceRenderer(MissedPresentation presentation)
    {
        Sprite sprite = FirstAssignedFrame(presentation);
        if (sprite == null || cctvCamera == null)
            return;

        if (serverFaceRenderer == null)
        {
            GameObject feedObject = new GameObject("D3_NR01_ServerFace_CCTVFeed");
            serverFaceRenderer = feedObject.AddComponent<SpriteRenderer>();
            serverFaceRenderer.sortingOrder = 32767;
            serverFaceRenderer.enabled = false;
        }

        serverFaceRenderer.sprite = sprite;
        Transform feedTransform = serverFaceRenderer.transform;
        feedTransform.SetParent(cctvCamera.transform, false);

        float distance = Mathf.Max(cctvCamera.nearClipPlane + 0.1f, 1f);
        feedTransform.localPosition = new Vector3(0f, 0f, distance);
        feedTransform.localRotation = Quaternion.identity;

        float viewHeight = cctvCamera.orthographic
            ? cctvCamera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cctvCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cctvCamera.aspect;
        Vector3 spriteSize = sprite.bounds.size;
        feedTransform.localScale = new Vector3(
            viewWidth / Mathf.Max(0.0001f, spriteSize.x),
            viewHeight / Mathf.Max(0.0001f, spriteSize.y),
            1f);
    }

    private static Sprite FirstAssignedFrame(MissedPresentation presentation)
    {
        if (presentation?.frames == null)
            return null;
        foreach (Sprite frame in presentation.frames)
        {
            if (frame != null)
                return frame;
        }
        return null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;
        if (sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(clip);
        else
            AudioSource.PlayClipAtPoint(clip, cctvCamera != null ? cctvCamera.transform.position : Vector3.zero);
    }

    private void HandleMissedAnomaly(AnomalyRuntime runtime)
    {
        // Update가 해당 미보고 횟수를 보고 각 구역/viewport 조건에서 한 번씩 판정합니다.
    }

    private void HandleDayStarted()
    {
        ResetPresentations();
    }

    private void ResetPresentations()
    {
        StopActiveRoutines();
        ResetPresentation(serverFace);
        ResetPresentation(ceilingPerson);
        ResetPresentation(reflectionPerson);
        SetServerFaceActive(false);
        RestoreAllEffectRoots();
    }

    private static void ResetPresentation(MissedPresentation presentation)
    {
        if (presentation == null)
            return;
        presentation.played = false;
        presentation.lastAttemptedMissedCount = 0;
    }

    private void RestoreAllEffectRoots()
    {
        RestoreEffectRoot(ceilingPerson);
        RestoreReflectionRoot();
    }

    private void RestoreEffectRoot(MissedPresentation presentation)
    {
        if (areaView == null || presentation == null || string.IsNullOrWhiteSpace(presentation.effectObjectId) ||
            !areaView.TryGetAreaInstance(presentation.areaId, out CCTVAreaInstance instance) || instance == null ||
            !instance.TryGetObject(presentation.effectObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
            return;

        sceneObject.gameObject.SetActive(false);
    }

    private void RestoreReflectionRoot()
    {
        if (areaView == null || reflectionPerson == null ||
            !areaView.TryGetAreaInstance(reflectionPerson.areaId, out CCTVAreaInstance instance) || instance == null ||
            !instance.TryGetObject(reflectionPerson.effectObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
            return;

        SpriteRenderer renderer = sceneObject.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && reflectionNormalSprite != null)
            renderer.sprite = reflectionNormalSprite;
        sceneObject.gameObject.SetActive(true);
    }

    private void StopActiveRoutines()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        if (pendingTimedRoutine != null)
            StopCoroutine(pendingTimedRoutine);
        activeRoutine = null;
        pendingTimedRoutine = null;
    }

    private void SetServerFaceActive(bool active)
    {
        if (serverFaceRenderer != null)
            serverFaceRenderer.enabled = active;
    }

    private void Subscribe()
    {
        if (dayRuntimeController == null || subscribedRuntime == dayRuntimeController)
            return;
        Unsubscribe();
        subscribedRuntime = dayRuntimeController;
        subscribedRuntime.MissedAnomalyRegistered += HandleMissedAnomaly;
        subscribedRuntime.DayStarted += HandleDayStarted;
    }

    private void Unsubscribe()
    {
        if (subscribedRuntime == null)
            return;
        subscribedRuntime.MissedAnomalyRegistered -= HandleMissedAnomaly;
        subscribedRuntime.DayStarted -= HandleDayStarted;
        subscribedRuntime = null;
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (areaView == null)
            areaView = FindFirstObjectByType<CCTVAreaView>();
        if (panController == null)
            panController = FindFirstObjectByType<CCTVPanController>();
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

    private void OnDestroy()
    {
        if (serverFaceRenderer == null)
            return;
        if (Application.isPlaying)
            Destroy(serverFaceRenderer.gameObject);
        else
            DestroyImmediate(serverFaceRenderer.gameObject);
    }
}
