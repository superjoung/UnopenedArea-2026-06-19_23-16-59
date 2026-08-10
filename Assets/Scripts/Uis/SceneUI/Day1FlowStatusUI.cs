using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Day1FlowController의 상태와 안내 문구를 UI에 표시한다.
///
/// Canvas 배치와 그래픽은 이 컴포넌트가 담당하지 않는다. Inspector에서 텍스트,
/// 안내 패널, 정전 오버레이를 원하는 위치의 UI 오브젝트에 연결해 사용한다.
/// 버튼의 OnClick에는 HandlePrimaryAction 또는 CompleteEmergencyObjective를 연결한다.
/// </summary>
public class Day1FlowStatusUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController flowController;

    [Header("Always-visible Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Objective / Guidance")]
    [SerializeField] private TMP_Text objectiveText;

    [Header("Field Objective / Guidance")]
    [SerializeField] private TMP_Text fieldStatusText;
    [SerializeField] private TMP_Text fieldObjectiveText;

    [Header("Report Feedback")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField, Min(0.1f)] private float feedbackDuration = 2f;

    [Header("Emergency")]
    [SerializeField] private GameObject emergencyOverlay;

    [Header("Optional Primary Action Button")]
    [SerializeField] private GameObject primaryActionRoot;
    [SerializeField] private TMP_Text primaryActionLabel;

    private Day1FlowController subscribedFlowController;
    private GameManager subscribedGameManager;
    private Coroutine feedbackCoroutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        SubscribeGameManager();
    }

    private void Start()
    {
        Refresh(flowController != null ? flowController.State : Day1FlowState.None);
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeGameManager();
    }

    /// <summary>
    /// 하나의 "다음 / 감시 시작 / 복구" 버튼에 연결한다.
    /// 상태에 따라 적절한 Day 1 공개 메서드를 호출한다.
    /// </summary>
    public void HandlePrimaryAction()
    {
        if (flowController == null)
            return;

        switch (flowController.State)
        {
            case Day1FlowState.Briefing:
                flowController.BeginBaselineReview();
                break;
            case Day1FlowState.BaselineReview:
                flowController.BeginMonitoring();
                break;
            case Day1FlowState.EmergencyDispatch:
                flowController.CompleteEmergencyObjective();
                break;
        }
    }

    /// <summary>
    /// 정전 복구 전용 버튼에 연결할 수 있는 명시적 진입점이다.
    /// </summary>
    public void CompleteEmergencyObjective()
    {
        if (flowController != null)
            flowController.CompleteEmergencyObjective();
    }

    private void HandleStateChanged(Day1FlowState state)
    {
        Refresh(state);
    }

    private void HandleFlowMessageChanged(string message)
    {
        bool useFieldGuidance = ShouldUseFieldGuidance();
        SetGuidanceVisibility(useFieldGuidance);

        TMP_Text targetObjectiveText = useFieldGuidance ? fieldObjectiveText : objectiveText;
        if (targetObjectiveText != null)
            targetObjectiveText.text = message;

        TMP_Text targetStatusText = useFieldGuidance ? fieldStatusText : statusText;
        if (targetStatusText != null && flowController != null)
            targetStatusText.text = GetStatusLabel(flowController.State);
    }

    private void HandleCorrectReport(AnomalyRuntime runtime)
    {
        ShowFeedback("정상 보고 처리됨", new Color(0.57f, 1f, 0.72f));
    }

    private void HandleWrongReport()
    {
        ShowFeedback("오보고 · 실패 +1", new Color(1f, 0.48f, 0.44f));
    }

    private void HandleMissedAnomaly(AnomalyRuntime runtime)
    {
        string correctedAnomaly = GetCorrectedAnomalyLabel(runtime);
        ShowFeedback($"미보고 +1\n{correctedAnomaly}", new Color(1f, 0.72f, 0.36f));
    }

    private static string GetCorrectedAnomalyLabel(AnomalyRuntime runtime)
    {
        AnomalyDefinition definition = runtime != null ? runtime.Definition : null;
        if (definition == null)
            return "이상현상이 수정되었습니다.";

        string area = CCTVReportLabelProvider.GetAreaLabel(definition.AreaId);
        string target = CCTVReportLabelProvider.GetTargetLabel(definition.ReportTargetId);
        string type = CCTVReportLabelProvider.GetReportTypeLabel(definition.ReportType);
        return $"수정: {area} · {target} · {type}";
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;

        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackCoroutine = StartCoroutine(ClearFeedbackRoutine());
    }

    private IEnumerator ClearFeedbackRoutine()
    {
        yield return new WaitForSecondsRealtime(feedbackDuration);

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        feedbackCoroutine = null;
    }

    private void Refresh(Day1FlowState state)
    {
        bool useFieldGuidance = ShouldUseFieldGuidance();
        SetGuidanceVisibility(useFieldGuidance);

        TMP_Text targetStatusText = useFieldGuidance ? fieldStatusText : statusText;
        if (targetStatusText != null)
            targetStatusText.text = GetStatusLabel(state);

        bool isEmergency = state == Day1FlowState.EmergencyDispatch ||
                           state == Day1FlowState.EmergencyRecovery;
        if (emergencyOverlay != null)
            emergencyOverlay.SetActive(isEmergency);

        bool canUsePrimaryAction = state == Day1FlowState.Briefing ||
                                   state == Day1FlowState.BaselineReview ||
                                   state == Day1FlowState.EmergencyDispatch;
        if (primaryActionRoot != null)
            primaryActionRoot.SetActive(canUsePrimaryAction);

        if (primaryActionLabel != null)
            primaryActionLabel.text = GetPrimaryActionLabel(state);
    }

    private string GetStatusLabel(Day1FlowState state)
    {
        int day = flowController != null && flowController.DayNumber > 0
            ? flowController.DayNumber
            : 1;

        switch (state)
        {
            case Day1FlowState.Briefing:
                return $"DAY {day} · 브리핑";
            case Day1FlowState.BaselineReview:
                return $"DAY {day} · 기준 상태 확인";
            case Day1FlowState.Monitoring:
                return $"DAY {day} · 감시 중";
            case Day1FlowState.EmergencyDispatch:
                return $"DAY {day} · 정전 발생";
            case Day1FlowState.EmergencyRecovery:
                return $"DAY {day} · 전력 복구 중";
            case Day1FlowState.Completed:
                return $"DAY {day} · 근무 종료";
            case Day1FlowState.Failed:
                return $"DAY {day} · 근무 실패";
            default:
                return string.Empty;
        }
    }

    private static string GetPrimaryActionLabel(Day1FlowState state)
    {
        switch (state)
        {
            case Day1FlowState.Briefing:
                return "다음";
            case Day1FlowState.BaselineReview:
                return "감시 시작";
            case Day1FlowState.EmergencyDispatch:
                return "전력 복구";
            default:
                return string.Empty;
        }
    }

    private void Subscribe()
    {
        if (flowController == null || subscribedFlowController == flowController)
            return;

        Unsubscribe();
        subscribedFlowController = flowController;
        subscribedFlowController.StateChanged += HandleStateChanged;
        subscribedFlowController.FlowMessageChanged += HandleFlowMessageChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedFlowController == null)
            return;

        subscribedFlowController.StateChanged -= HandleStateChanged;
        subscribedFlowController.FlowMessageChanged -= HandleFlowMessageChanged;
        subscribedFlowController = null;
    }

    private void SubscribeGameManager()
    {
        GameManager sceneGameManager = FindFirstObjectByType<GameManager>();
        if (sceneGameManager == null || subscribedGameManager == sceneGameManager)
            return;

        UnsubscribeGameManager();
        subscribedGameManager = sceneGameManager;
        subscribedGameManager.DayCorrectReport += HandleCorrectReport;
        subscribedGameManager.DayWrongReport += HandleWrongReport;
        subscribedGameManager.DayMissedAnomaly += HandleMissedAnomaly;
    }

    private void UnsubscribeGameManager()
    {
        if (subscribedGameManager == null)
            return;

        subscribedGameManager.DayCorrectReport -= HandleCorrectReport;
        subscribedGameManager.DayWrongReport -= HandleWrongReport;
        subscribedGameManager.DayMissedAnomaly -= HandleMissedAnomaly;
        subscribedGameManager = null;
    }

    private void ResolveReferences()
    {
        if (flowController == null)
            flowController = FindObjectOfType<Day1FlowController>();
    }

    private bool ShouldUseFieldGuidance()
    {
        return flowController != null && flowController.IsEmergencyFieldModeStarted;
    }

    private void SetGuidanceVisibility(bool useFieldGuidance)
    {
        SetTextActive(statusText, !useFieldGuidance);
        SetTextActive(objectiveText, !useFieldGuidance);
        SetTextActive(fieldStatusText, useFieldGuidance);
        SetTextActive(fieldObjectiveText, useFieldGuidance);
    }

    private static void SetTextActive(TMP_Text text, bool active)
    {
        if (text != null && text.gameObject.activeSelf != active)
            text.gameObject.SetActive(active);
    }
}
