using UnityEngine;

public enum Day1FlowState
{
    None = 0,
    Briefing = 1,
    BaselineReview = 2,
    Monitoring = 3,
    EmergencyDispatch = 4,
    EmergencyRecovery = 5,
    Completed = 6,
    Failed = 7,
}

/// <summary>
/// Day 1의 고정 진행을 담당한다. UI가 연결되기 전에는 Enter 키로 브리핑과
/// 기준 상태 확인을 넘길 수 있으며, 이후 UI는 공개 메서드를 호출하면 된다.
/// </summary>
public class Day1FlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private AnomalyScheduler anomalyScheduler;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private CCTVTestSceneController sceneController;

    [Header("Temporary Debug Input")]
    [SerializeField] private bool useKeyboardAdvance = true;
    [SerializeField] private KeyCode emergencyRecoveryKey = KeyCode.E;

    private DayDefinition dayDefinition;
    private int channelSwitchCount;
    private bool tutorialActivationRequested;
    private bool tutorialFinished;
    private bool emergencyDispatchStarted;

    public Day1FlowState State { get; private set; } = Day1FlowState.None;
    public int ChannelSwitchCount => channelSwitchCount;
    public bool TutorialFinished => tutorialFinished;

    public System.Action<Day1FlowState> StateChanged;
    public System.Action<string> FlowMessageChanged;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        if (dayRuntimeController == null || dayRuntimeController.CurrentDayDefinition == null)
        {
            Debug.LogWarning("[Day1FlowController] DayDefinition is missing. Day 1 flow is disabled.");
            enabled = false;
            return;
        }

        dayDefinition = dayRuntimeController.CurrentDayDefinition;
        if (dayDefinition.Day != 1)
        {
            enabled = false;
            return;
        }

        SubscribeEvents();
        PauseNormalAnomalies();
        ChangeState(Day1FlowState.Briefing, "제4관측동 폐쇄 전 상태 기록을 시작합니다. Enter 키로 브리핑을 진행합니다.");
    }

    private void Update()
    {
        if (useKeyboardAdvance && Input.GetKeyDown(KeyCode.Return))
        {
            if (State == Day1FlowState.Briefing)
                BeginBaselineReview();
            else if (State == Day1FlowState.BaselineReview)
                BeginMonitoring();
        }

        if (State == Day1FlowState.Monitoring)
        {
            if (!tutorialActivationRequested)
                TryStartTutorialAnomaly();
            else if (tutorialFinished)
                TryStartEmergencyDispatch();
        }

        if (useKeyboardAdvance && State == Day1FlowState.EmergencyDispatch && Input.GetKeyDown(emergencyRecoveryKey))
            CompleteEmergencyObjective();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void BeginBaselineReview()
    {
        if (State != Day1FlowState.Briefing)
            return;

        ChangeState(Day1FlowState.BaselineReview, "모든 CCTV의 정상 배치를 확인하십시오. Enter 키로 감시를 시작합니다.");
    }

    public void BeginMonitoring()
    {
        if (State != Day1FlowState.BaselineReview)
            return;

        channelSwitchCount = 0;
        tutorialActivationRequested = false;
        tutorialFinished = false;
        emergencyDispatchStarted = false;

        if (dayRuntimeController != null)
            dayRuntimeController.StartDay();

        // 첫 튜토리얼 이상현상이 끝날 때까지 랜덤 이벤트는 시작하지 않는다.
        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(true);

        ChangeState(Day1FlowState.Monitoring, "감시를 시작합니다. CCTV 채널을 전환해 정상 배치를 확인하십시오.");
    }

    private void TryStartTutorialAnomaly()
    {
        if (dayDefinition == null || dayDefinition.TutorialAnomaly == null || anomalyService == null)
            return;

        bool reachedChannelCondition = channelSwitchCount >= dayDefinition.TutorialRequiredChannelSwitches;
        bool reachedTimeCondition = dayRuntimeController != null &&
                                    dayRuntimeController.ElapsedSec >= dayDefinition.TutorialFallbackElapsedSec;

        if (!reachedChannelCondition && !reachedTimeCondition)
            return;

        AnomalyRuntime runtime = anomalyService.Activate(dayDefinition.TutorialAnomaly);
        if (runtime == null)
            return;

        tutorialActivationRequested = true;
        FlowMessageChanged?.Invoke("기준 화면과 일치하지 않는 항목을 보고하십시오.");
        Debug.Log($"[Day1FlowController] Tutorial anomaly started. id={dayDefinition.TutorialAnomaly.AnomalyId}, channelSwitches={channelSwitchCount}, elapsed={dayRuntimeController.ElapsedSec:F1}");
    }

    private void HandleChannelSelected(CCTVChannelRuntime channel)
    {
        if (State != Day1FlowState.Monitoring || tutorialActivationRequested)
            return;

        channelSwitchCount++;
        Debug.Log($"[Day1FlowController] Tutorial channel switch progress {channelSwitchCount}/{dayDefinition.TutorialRequiredChannelSwitches}. channel={channel?.ChannelLabel}");
    }

    private void HandleAnomalyResolved(AnomalyRuntime runtime)
    {
        if (!IsTutorialRuntime(runtime))
            return;

        FinishTutorial(true);
    }

    private void HandleAnomalyMissed(AnomalyRuntime runtime)
    {
        if (!IsTutorialRuntime(runtime))
            return;

        FinishTutorial(false);
    }

    private bool IsTutorialRuntime(AnomalyRuntime runtime)
    {
        return !tutorialFinished &&
               tutorialActivationRequested &&
               runtime != null &&
               runtime.Definition == dayDefinition.TutorialAnomaly;
    }

    private void FinishTutorial(bool resolved)
    {
        tutorialFinished = true;

        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(false);

        string result = resolved ? "첫 기록이 정상적으로 처리되었습니다." : "첫 기록이 누락되었습니다. 감시를 계속하십시오.";
        FlowMessageChanged?.Invoke(result);
        Debug.Log($"[Day1FlowController] Tutorial finished. resolved={resolved}");
    }

    private void TryStartEmergencyDispatch()
    {
        if (emergencyDispatchStarted || dayDefinition == null || !dayDefinition.EnableEmergencyDispatch)
            return;

        if (dayRuntimeController == null || dayRuntimeController.State != DayRuntimeState.Running)
            return;

        bool hasRequiredReports = dayRuntimeController.SuccessReportCount >= dayDefinition.EmergencyRequiredCorrectReports;
        float progress = dayRuntimeController.DurationSec <= 0f
            ? 0f
            : dayRuntimeController.ElapsedSec / dayRuntimeController.DurationSec;
        bool reachedProgress = progress >= dayDefinition.EmergencyTriggerProgress;

        if (!hasRequiredReports || !reachedProgress)
            return;

        emergencyDispatchStarted = true;
        PauseForEmergencyDispatch();
        ChangeState(
            Day1FlowState.EmergencyDispatch,
            $"정전 발생. 현장 복구가 필요합니다. 임시 테스트에서는 {emergencyRecoveryKey} 키로 배전반을 복구합니다."
        );
    }

    /// <summary>
    /// 현장 맵의 배전반 상호작용이 호출할 복구 완료 진입점이다.
    /// 현장 맵이 준비되기 전에는 emergencyRecoveryKey로 같은 흐름을 검증한다.
    /// </summary>
    public void CompleteEmergencyObjective()
    {
        if (State != Day1FlowState.EmergencyDispatch)
            return;

        ChangeState(Day1FlowState.EmergencyRecovery, "배전반 복구 완료. CCTV 전력을 재가동합니다.");
        ResumeAfterEmergencyDispatch();
        ChangeState(Day1FlowState.Monitoring, "전력이 복구되었습니다. 감시 업무를 계속합니다.");
    }

    private void HandleDayCleared()
    {
        ChangeState(Day1FlowState.Completed, "근무 시간이 종료되었습니다.");
    }

    private void HandleDayFailed()
    {
        ChangeState(Day1FlowState.Failed, "인지 편차가 허용치를 초과했습니다.");
    }

    private void PauseNormalAnomalies()
    {
        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(true);

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);
    }

    private void PauseForEmergencyDispatch()
    {
        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(true);

        if (dayRuntimeController != null)
            dayRuntimeController.PauseDay();

        if (sceneController != null)
            sceneController.SetCCTVInputEnabled(false);

        if (GameManager.Instance != null)
            GameManager.Instance.SetReportInputEnabled(false);
    }

    private void ResumeAfterEmergencyDispatch()
    {
        if (dayRuntimeController != null)
            dayRuntimeController.ResumeDay();

        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(false);

        if (sceneController != null)
            sceneController.SetCCTVInputEnabled(true);

        if (GameManager.Instance != null)
            GameManager.Instance.SetReportInputEnabled(true);
    }

    private void ChangeState(Day1FlowState nextState, string message)
    {
        State = nextState;
        StateChanged?.Invoke(nextState);
        FlowMessageChanged?.Invoke(message);
        Debug.Log($"[Day1FlowController] State={nextState}. {message}");
    }

    private void SubscribeEvents()
    {
        if (sceneController != null)
            sceneController.ChannelSelected += HandleChannelSelected;

        if (anomalyService != null)
        {
            anomalyService.AnomalyResolved += HandleAnomalyResolved;
            anomalyService.AnomalyMissed += HandleAnomalyMissed;
        }

        if (dayRuntimeController != null)
        {
            dayRuntimeController.DayCleared += HandleDayCleared;
            dayRuntimeController.DayFailed += HandleDayFailed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (sceneController != null)
            sceneController.ChannelSelected -= HandleChannelSelected;

        if (anomalyService != null)
        {
            anomalyService.AnomalyResolved -= HandleAnomalyResolved;
            anomalyService.AnomalyMissed -= HandleAnomalyMissed;
        }

        if (dayRuntimeController != null)
        {
            dayRuntimeController.DayCleared -= HandleDayCleared;
            dayRuntimeController.DayFailed -= HandleDayFailed;
        }
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (anomalyScheduler == null)
            anomalyScheduler = FindObjectOfType<AnomalyScheduler>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();

        if (sceneController == null)
            sceneController = FindObjectOfType<CCTVTestSceneController>();
    }
}
