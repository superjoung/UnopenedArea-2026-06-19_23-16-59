using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day 2의 보고 불가 미보고 연출(D2_NR01~03)을 담당한다.
/// 각 연출은 실제 이상현상과 별개이며, 미보고 누적 횟수에만 반응한다.
/// </summary>
public class Day2MissedPresentationController : MonoBehaviour
{
    [System.Serializable]
    private sealed class ViewportIntrusion
    {
        [Tooltip("이 횟수의 미보고가 누적된 뒤부터 연출을 대기합니다. NR01=1, NR03=3")]
        [Min(1)] public int requiredMissedCount = 1;
        public AreaId areaId = AreaId.TreatmentRoom;
        [Tooltip("치료실 프리팹 안 CCTVSceneObject의 Object Id입니다. NR01=OBJ_TREAT_CURTAIN_HAND_01, NR03=OBJ_TREAT_UNDERBED_PERSON_01")]
        public string effectObjectId;
        [Tooltip("화면 가로 중앙 판정 범위입니다. CCTV는 좌우로만 이동하므로 Y 위치는 판정하지 않습니다.")]
        [Range(0.01f, 0.5f)] public float centerTolerance = 0.1f;
        [Min(0.1f)] public float presentationDuration = 2f;
        [Range(0f, 1f)] public float probability = 0.7f;

        [HideInInspector] public bool played;
        [HideInInspector] public int lastAttemptedMissedCount;
    }

    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVPanController panController;
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private Camera cctvCamera;

    [Header("D2_NR01 / D2_NR03 - Viewport Intrusions")]
    [SerializeField] private ViewportIntrusion[] viewportIntrusions;

    [Header("D2_NR02 - Screen Flash")]
    [Tooltip("CommonRoot/CCTVSceneUI 안에 둘 얼굴 UI 루트 이름입니다. 비활성 오브젝트도 이름으로 찾습니다.")]
    [SerializeField] private string faceFlashObjectName = "D2FaceFlash";
    [Tooltip("실제 CCTV 렌더 카메라를 찾기 위한 런타임 RawImage 이름입니다. 얼굴은 이 카메라 피드 안에 그려져 CRT 쉐이더의 영향을 받습니다.")]
    [SerializeField] private string cctvRawImageName = "RawImage_CCTVScreen";
    [Tooltip("얼굴이 노이즈 위에 완전히 보이는 유지 시간입니다.")]
    [SerializeField, Min(0f)] private float faceFlashHoldDuration = 3f;
    [Tooltip("얼굴 Image들의 알파를 직접 낮춰 사라지는 시간입니다.")]
    [SerializeField, Min(0.05f)] private float faceFlashFadeDuration = 0.5f;
    [SerializeField, Min(1)] private int faceFlashRequiredMissedCount = 2;

    private DayRuntimeController subscribedRuntime;
    private Coroutine activePresentationRoutine;
    private bool faceFlashPlayed;

    private GameObject faceFlashRoot;
    private Image faceFlashSourceImage;
    private SpriteRenderer faceFlashFeedRenderer;
    private Color faceFlashFeedBaseColor = Color.white;
    private readonly List<Graphic> faceFlashGraphics = new List<Graphic>();
    private readonly List<Color> faceFlashBaseColors = new List<Color>();

    private void Awake()
    {
        ResolveReferences();
        EnsureDefaultViewportIntrusions();
        ResolveFaceFlashRoot();
        CacheFaceFlashGraphics();
        SetActive(faceFlashRoot, false);
        ResetViewportIntrusions();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (activePresentationRoutine != null)
            StopCoroutine(activePresentationRoutine);
        activePresentationRoutine = null;
        sceneController?.SetCCTVInputEnabled(true);
        panController?.SetInputLocked(false);
        SetFaceFeedActive(false);
    }

    private void Update()
    {
        if (activePresentationRoutine != null || dayRuntimeController == null ||
            dayRuntimeController.MissedAnomalyCount <= 0 || areaView == null)
            return;

        if (viewportIntrusions == null)
            return;

        for (int i = 0; i < viewportIntrusions.Length; i++)
        {
            ViewportIntrusion intrusion = viewportIntrusions[i];
            if (intrusion == null || intrusion.played ||
                intrusion.lastAttemptedMissedCount >= dayRuntimeController.MissedAnomalyCount ||
                dayRuntimeController.MissedAnomalyCount < intrusion.requiredMissedCount ||
                areaView.CurrentArea == null || areaView.CurrentArea.AreaId != intrusion.areaId ||
                !TryGetCurrentEffectRoot(intrusion, out GameObject effectRoot) ||
                !IsEffectAtViewportCenter(effectRoot.transform, intrusion.centerTolerance))
                continue;

            intrusion.lastAttemptedMissedCount = dayRuntimeController.MissedAnomalyCount;
            bool success = Random.value <= intrusion.probability;
            Debug.Log($"[Day2MissedPresentationController] chance event={intrusion.effectObjectId}, probability={intrusion.probability:0.00}, success={success}", this);
            if (!success)
                continue;

            activePresentationRoutine = StartCoroutine(PlayViewportIntrusion(intrusion));
            break;
        }
    }

    private void HandleMissedAnomaly(AnomalyRuntime runtime)
    {
        if (dayRuntimeController == null || faceFlashPlayed ||
            dayRuntimeController.MissedAnomalyCount < faceFlashRequiredMissedCount)
            return;

        faceFlashPlayed = true;
        StartCoroutine(PlayFaceFlash());
    }

    private void HandleDayStarted()
    {
        faceFlashPlayed = false;
        SetActive(faceFlashRoot, false);
        SetFaceFeedActive(false);
        ResetViewportIntrusions();
    }

    private IEnumerator PlayFaceFlash()
    {
        ResolveFaceFlashRoot();
        if (faceFlashRoot == null)
            yield break;

        CacheFaceFlashGraphics();
        PrepareFaceFlashFeedRenderer();
        if (faceFlashFeedRenderer == null)
        {
            Debug.LogError("[Day2MissedPresentationController] FaceImg 또는 CCTV 렌더 카메라를 찾지 못해 D2_NR02를 재생할 수 없습니다.", this);
            yield break;
        }

        SetActive(faceFlashRoot, false);
        SetFaceFeedActive(true);
        ApplyFaceFlashAlpha(1f);

        if (faceFlashHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(faceFlashHoldDuration);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, faceFlashFadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFaceFlashAlpha(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        ApplyFaceFlashAlpha(0f);
        SetFaceFeedActive(false);
        SetActive(faceFlashRoot, false);
        ApplyFaceFlashAlpha(1f);
    }

    private IEnumerator PlayViewportIntrusion(ViewportIntrusion intrusion)
    {
        intrusion.played = true;

        // 카메라가 대상 중앙을 잡은 상태를 유지해, 짧은 애니메이션을 확실히 보이게 한다.
        panController?.SetInputLocked(true);
        sceneController?.SetCCTVInputEnabled(false);

        if (!TryGetCurrentEffectRoot(intrusion, out GameObject effectRoot))
        {
            activePresentationRoutine = null;
            sceneController?.SetCCTVInputEnabled(true);
            panController?.SetInputLocked(false);
            yield break;
        }

        SetActive(effectRoot, true);
        Animator animator = effectRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        yield return new WaitForSecondsRealtime(intrusion.presentationDuration);

        if (animator != null)
            animator.enabled = false;
        SetActive(effectRoot, false);

        sceneController?.SetCCTVInputEnabled(true);
        panController?.SetInputLocked(false);
        activePresentationRoutine = null;
    }

    private bool IsEffectAtViewportCenter(Transform effectTransform, float tolerance)
    {
        if (cctvCamera == null || effectTransform == null)
            return false;

        Vector3 viewport = cctvCamera.WorldToViewportPoint(effectTransform.position);
        return viewport.z > 0f && Mathf.Abs(viewport.x - 0.5f) <= tolerance;
    }

    private void ResetViewportIntrusions()
    {
        if (viewportIntrusions == null)
            return;

        foreach (ViewportIntrusion intrusion in viewportIntrusions)
        {
            if (intrusion == null)
                continue;

            intrusion.played = false;
            intrusion.lastAttemptedMissedCount = 0;
            if (TryGetEffectRootInAnyPreparedArea(intrusion, out GameObject effectRoot))
            {
                Animator animator = effectRoot.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    animator.enabled = false;
                SetActive(effectRoot, false);
            }
        }
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
        if (cctvCamera == null)
            cctvCamera = Camera.main;
    }

    private void EnsureDefaultViewportIntrusions()
    {
        if (viewportIntrusions != null && viewportIntrusions.Length > 0)
            return;

        viewportIntrusions = new[]
        {
            new ViewportIntrusion
            {
                requiredMissedCount = 1,
                areaId = AreaId.TreatmentRoom,
                effectObjectId = "OBJ_TREAT_CURTAIN_HAND_01",
                centerTolerance = 0.1f,
                presentationDuration = 2f,
                probability = 0.7f,
            },
            new ViewportIntrusion
            {
                requiredMissedCount = 3,
                areaId = AreaId.TreatmentRoom,
                effectObjectId = "OBJ_TREAT_UNDERBED_PERSON_01",
                centerTolerance = 0.1f,
                presentationDuration = 2f,
                probability = 0.7f,
            },
        };
    }

    private bool TryGetCurrentEffectRoot(ViewportIntrusion intrusion, out GameObject effectRoot)
    {
        effectRoot = null;
        CCTVAreaInstance instance = areaView != null ? areaView.CurrentInstance : null;
        if (instance == null || string.IsNullOrWhiteSpace(intrusion.effectObjectId) ||
            !instance.TryGetObject(intrusion.effectObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
            return false;

        effectRoot = sceneObject.gameObject;
        return true;
    }

    private bool TryGetEffectRootInAnyPreparedArea(ViewportIntrusion intrusion, out GameObject effectRoot)
    {
        effectRoot = null;
        if (areaView == null || string.IsNullOrWhiteSpace(intrusion.effectObjectId) ||
            !areaView.TryGetAreaInstance(intrusion.areaId, out CCTVAreaInstance instance) || instance == null ||
            !instance.TryGetObject(intrusion.effectObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
            return false;

        effectRoot = sceneObject.gameObject;
        return true;
    }

    private void ResolveFaceFlashRoot()
    {
        if (faceFlashRoot != null || string.IsNullOrWhiteSpace(faceFlashObjectName))
            return;

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == faceFlashObjectName)
            {
                faceFlashRoot = candidate.gameObject;
                faceFlashSourceImage = faceFlashRoot.GetComponentInChildren<Image>(true);
                if (faceFlashSourceImage != null)
                    faceFlashFeedBaseColor = faceFlashSourceImage.color;
                return;
            }
        }
    }

    private void PrepareFaceFlashFeedRenderer()
    {
        if (faceFlashSourceImage == null || faceFlashSourceImage.sprite == null || string.IsNullOrWhiteSpace(cctvRawImageName))
            return;

        Transform rawImageTransform = null;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == cctvRawImageName)
            {
                rawImageTransform = candidate;
                break;
            }
        }

        if (rawImageTransform == null)
            return;

        // UI sibling 순서로는 얼굴이 CRT 셰이더의 앞이나 뒤로만 빠진다.
        // 실제 CCTV 렌더 카메라 안에 SpriteRenderer를 만들어 RenderTexture와 함께 후처리한다.
        CCTVScreenEffectController effectController = rawImageTransform.GetComponent<CCTVScreenEffectController>();
        Camera renderCamera = effectController != null && effectController.WorldCamera != null
            ? effectController.WorldCamera
            : cctvCamera;
        if (renderCamera == null)
            return;

        cctvCamera = renderCamera;
        if (faceFlashFeedRenderer == null)
        {
            GameObject feedObject = new GameObject("D2_FaceFlash_CCTVFeed");
            faceFlashFeedRenderer = feedObject.AddComponent<SpriteRenderer>();
            faceFlashFeedRenderer.sortingOrder = 32767;
            faceFlashFeedRenderer.enabled = false;
        }

        faceFlashFeedRenderer.sprite = faceFlashSourceImage.sprite;
        faceFlashFeedBaseColor = faceFlashSourceImage.color;
        faceFlashFeedRenderer.color = faceFlashFeedBaseColor;
        UpdateFaceFlashFeedTransform(renderCamera);
    }

    private void UpdateFaceFlashFeedTransform(Camera renderCamera)
    {
        if (faceFlashFeedRenderer == null || faceFlashFeedRenderer.sprite == null || renderCamera == null)
            return;

        Transform feedTransform = faceFlashFeedRenderer.transform;
        if (feedTransform.parent != renderCamera.transform)
            feedTransform.SetParent(renderCamera.transform, false);

        float distance = Mathf.Max(renderCamera.nearClipPlane + 0.1f, 1f);
        feedTransform.localPosition = new Vector3(0f, 0f, distance);
        feedTransform.localRotation = Quaternion.identity;

        float viewHeight;
        if (renderCamera.orthographic)
        {
            viewHeight = renderCamera.orthographicSize * 2f;
        }
        else
        {
            viewHeight = 2f * distance * Mathf.Tan(renderCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        float viewWidth = viewHeight * renderCamera.aspect;
        Vector3 spriteSize = faceFlashFeedRenderer.sprite.bounds.size;
        float scaleX = viewWidth / Mathf.Max(0.0001f, spriteSize.x);
        float scaleY = viewHeight / Mathf.Max(0.0001f, spriteSize.y);
        feedTransform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private void CacheFaceFlashGraphics()
    {
        if (faceFlashRoot == null || faceFlashGraphics.Count > 0)
            return;

        foreach (Graphic graphic in faceFlashRoot.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
                continue;

            faceFlashGraphics.Add(graphic);
            faceFlashBaseColors.Add(graphic.color);
        }
    }

    private void ApplyFaceFlashAlpha(float multiplier)
    {
        if (faceFlashFeedRenderer != null)
        {
            Color color = faceFlashFeedBaseColor;
            color.a *= Mathf.Clamp01(multiplier);
            faceFlashFeedRenderer.color = color;
            return;
        }

        for (int i = 0; i < faceFlashGraphics.Count; i++)
        {
            Graphic graphic = faceFlashGraphics[i];
            if (graphic == null)
                continue;

            Color color = i < faceFlashBaseColors.Count ? faceFlashBaseColors[i] : graphic.color;
            color.a *= Mathf.Clamp01(multiplier);
            graphic.color = color;
        }
    }

    private void SetFaceFeedActive(bool active)
    {
        if (faceFlashFeedRenderer != null)
            faceFlashFeedRenderer.enabled = active;
    }

    private void OnDestroy()
    {
        if (faceFlashFeedRenderer == null)
            return;

        if (Application.isPlaying)
            Destroy(faceFlashFeedRenderer.gameObject);
        else
            DestroyImmediate(faceFlashFeedRenderer.gameObject);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
