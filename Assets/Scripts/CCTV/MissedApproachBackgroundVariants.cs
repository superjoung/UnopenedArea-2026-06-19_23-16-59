using UnityEngine;

/// <summary>
/// 미보고 누적 단계에 따라 맵 배경 Variant 하나만 표시합니다.
/// CCTV Area 프리팹과 Day 1 현장 배경 모두에 붙여 사용할 수 있습니다.
/// </summary>
public class MissedApproachBackgroundVariants : MonoBehaviour
{
    [Header("Background Variants")]
    [Tooltip("미보고 0회(정상) 배경입니다.")]
    [SerializeField] private GameObject missed0Background;
    [Tooltip("미보고 1회(침투 시작) 배경입니다.")]
    [SerializeField] private GameObject missed1Background;
    [Tooltip("미보고 2회 이상(제어실 접근) 배경입니다.")]
    [SerializeField] private GameObject missed2Background;

    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;

    [Tooltip("3회 미보고는 별도 CCTVRoom 탈취 연출이므로 이 배경 세트에서는 2회 배경을 유지합니다.")]
    [SerializeField] private bool useStage2ForThreeOrMoreMisses = true;

    private DayRuntimeController subscribedDayRuntimeController;

    private void Awake()
    {
        ResolveReferences();
        ApplyCurrentMissedCount();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        ApplyCurrentMissedCount();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void HandleMissedAnomalyRegistered(AnomalyRuntime runtime)
    {
        ApplyCurrentMissedCount();
    }

    private void HandleDayStarted()
    {
        ApplyStage(0);
    }

    public void ApplyCurrentMissedCount()
    {
        int missedCount = dayRuntimeController != null ? dayRuntimeController.MissedAnomalyCount : 0;
        ApplyStage(missedCount);
    }

    /// <summary>Inspector Button/Event에서 직접 단계별 배경을 확인할 때도 사용할 수 있습니다.</summary>
    public void ApplyStage(int missedCount)
    {
        int stage = Mathf.Max(0, missedCount);
        if (stage >= 3 && useStage2ForThreeOrMoreMisses)
            stage = 2;
        else
            stage = Mathf.Clamp(stage, 0, 2);

        SetActive(missed0Background, stage == 0);
        SetActive(missed1Background, stage == 1);
        SetActive(missed2Background, stage == 2);
    }

    private void Subscribe()
    {
        if (dayRuntimeController == null || subscribedDayRuntimeController == dayRuntimeController)
            return;

        Unsubscribe();
        subscribedDayRuntimeController = dayRuntimeController;
        subscribedDayRuntimeController.MissedAnomalyRegistered += HandleMissedAnomalyRegistered;
        subscribedDayRuntimeController.DayStarted += HandleDayStarted;
    }

    private void Unsubscribe()
    {
        if (subscribedDayRuntimeController == null)
            return;

        subscribedDayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
        subscribedDayRuntimeController.DayStarted -= HandleDayStarted;
        subscribedDayRuntimeController = null;
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
