using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미보고 확정 직전의 긴급감을 화면 최상단 비네트로 표현합니다.
/// GlobalEffectCanvas에 붙여 사용하며, 자식 Image들의 원래 크기를 기준으로 펄스합니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MissedAnomalyWarningOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;
    [Tooltip("평소에는 비활성화해 둘 비네트 묶음입니다. 이 컴포넌트가 붙은 GlobalEffectCanvas 자체는 켜 둡니다.")]
    [SerializeField] private GameObject effectRoot;
    [SerializeField] private List<RectTransform> vignetteLayers = new List<RectTransform>();

    [Header("Urgency Animation")]
    [SerializeField, Range(0f, 1f)] private float warningAlpha = 0.55f;
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.5f;
    [SerializeField, Min(1f)] private float pulseScale = 1.05f;
    [SerializeField, Min(0.05f)] private float pulseHalfDuration = 0.45f;

    [Header("Missed Finish")]
    [SerializeField, Range(0f, 1f)] private float missedFlashAlpha = 0.8f;
    [SerializeField, Min(0.05f)] private float missedFlashDuration = 0.12f;
    [SerializeField, Min(0.05f)] private float missedFadeOutDuration = 0.45f;

    private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly HashSet<AnomalyRuntime> urgentAnomalies = new HashSet<AnomalyRuntime>();
    private CanvasGroup canvasGroup;
    private AnomalyService subscribedService;
    private Day1AreaTransitionController subscribedAreaTransitionController;
    private bool suppressedForEmergencyTransition;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        ResolveReferences();
        ResolveEffectRoot();
        CacheLayers();
        SetHiddenImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        SubscribeAreaTransition();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeAreaTransition();
        KillActiveTweens();
        urgentAnomalies.Clear();
    }

    [ContextMenu("Preview Urgency")]
    public void PreviewUrgency()
    {
        BeginUrgency(null);
    }

    [ContextMenu("Clear Preview")]
    public void ClearPreview()
    {
        EndUrgency(null);
    }

    private void ResolveReferences()
    {
        if (anomalyService == null)
            anomalyService = FindFirstObjectByType<AnomalyService>();

        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();
    }

    private void ResolveEffectRoot()
    {
        if (effectRoot == null && transform.childCount > 0)
            effectRoot = transform.GetChild(0).gameObject;
    }

    private void CacheLayers()
    {
        if (vignetteLayers.Count == 0)
        {
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (image != null && image.rectTransform != transform)
                    vignetteLayers.Add(image.rectTransform);
            }
        }

        foreach (RectTransform layer in vignetteLayers)
        {
            if (layer != null && !baseScales.ContainsKey(layer))
                baseScales.Add(layer, layer.localScale);
        }
    }

    private void Subscribe()
    {
        if (anomalyService == null || subscribedService == anomalyService)
            return;

        Unsubscribe();
        subscribedService = anomalyService;
        subscribedService.AnomalyUrgencyStarted += BeginUrgency;
        subscribedService.AnomalyResolved += EndUrgency;
        subscribedService.AnomalyMissed += FinishMissed;
    }

    private void Unsubscribe()
    {
        if (subscribedService == null)
            return;

        subscribedService.AnomalyUrgencyStarted -= BeginUrgency;
        subscribedService.AnomalyResolved -= EndUrgency;
        subscribedService.AnomalyMissed -= FinishMissed;
        subscribedService = null;
    }

    private void SubscribeAreaTransition()
    {
        if (areaTransitionController == null || subscribedAreaTransitionController == areaTransitionController)
            return;

        UnsubscribeAreaTransition();
        subscribedAreaTransitionController = areaTransitionController;
        subscribedAreaTransitionController.ModeChanged += HandleAreaModeChanged;
    }

    private void UnsubscribeAreaTransition()
    {
        if (subscribedAreaTransitionController == null)
            return;

        subscribedAreaTransitionController.ModeChanged -= HandleAreaModeChanged;
        subscribedAreaTransitionController = null;
    }

    private void HandleAreaModeChanged(Day1AreaMode mode)
    {
        if (mode == Day1AreaMode.CCTV)
        {
            suppressedForEmergencyTransition = false;
            return;
        }

        urgentAnomalies.Clear();
        KillActiveTweens();
        SetHiddenImmediate();
    }

    /// <summary>
    /// 정전 및 현장 이동 연출이 시작될 때 진행 중인 미보고 경고를 즉시 제거하고,
    /// 다음 CCTV 진입 전까지 새 경고가 겹쳐 나오지 않도록 합니다.
    /// </summary>
    public void HideForEmergencyTransition()
    {
        suppressedForEmergencyTransition = true;
        urgentAnomalies.Clear();
        KillActiveTweens();
        SetHiddenImmediate();
    }

    private void BeginUrgency(AnomalyRuntime runtime)
    {
        if (suppressedForEmergencyTransition)
            return;

        if (runtime != null)
            urgentAnomalies.Add(runtime);

        SetEffectRootActive(true);
        KillActiveTweens();
        RestoreBaseScales();
        canvasGroup.alpha = 0f;
        AddTween(canvasGroup.DOFade(warningAlpha, fadeInDuration));

        foreach (KeyValuePair<RectTransform, Vector3> layer in baseScales)
        {
            Tween pulse = layer.Key.DOScale(layer.Value * pulseScale, pulseHalfDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            AddTween(pulse);
        }
    }

    private void EndUrgency(AnomalyRuntime runtime)
    {
        if (runtime != null)
            urgentAnomalies.Remove(runtime);

        if (urgentAnomalies.Count > 0)
            return;

        FadeOut(missedFadeOutDuration);
    }

    private void FinishMissed(AnomalyRuntime runtime)
    {
        if (runtime != null)
            urgentAnomalies.Remove(runtime);

        if (urgentAnomalies.Count > 0)
            return;

        KillActiveTweens();
        RestoreBaseScales();
        canvasGroup.alpha = Mathf.Max(canvasGroup.alpha, missedFlashAlpha);
        AddTween(canvasGroup.DOFade(0f, missedFadeOutDuration)
            .SetDelay(missedFlashDuration)
            .OnComplete(() => SetEffectRootActive(false)));
    }

    private void FadeOut(float duration)
    {
        KillActiveTweens();
        RestoreBaseScales();
        AddTween(canvasGroup.DOFade(0f, duration).OnComplete(() => SetEffectRootActive(false)));
    }

    private void SetHiddenImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        RestoreBaseScales();
        SetEffectRootActive(false);
    }

    private void SetEffectRootActive(bool active)
    {
        if (effectRoot != null && effectRoot.activeSelf != active)
            effectRoot.SetActive(active);
    }

    private void RestoreBaseScales()
    {
        foreach (KeyValuePair<RectTransform, Vector3> layer in baseScales)
        {
            if (layer.Key != null)
                layer.Key.localScale = layer.Value;
        }
    }

    private void AddTween(Tween tween)
    {
        if (tween != null)
            activeTweens.Add(tween);
    }

    private void KillActiveTweens()
    {
        foreach (Tween tween in activeTweens)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
        }

        activeTweens.Clear();
    }
}
