using UnityEngine;

public enum DayRuntimeState
{
    NotStarted = 0,
    Running = 1,
    Paused = 2,
    Cleared = 3,
    Failed = 4,
}

public enum DayFailureReason
{
    None = 0,
    WrongReports = 1,
    MissedAnomalies = 2,
}

public class DayRuntimeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayDefinition dayDefinition;
    [SerializeField] private AnomalyService anomalyService;

    [Header("Options")]
    [SerializeField] private bool autoStartOnStart = true;

    [Header("Failure Limits")]
    [Tooltip("오보고만으로 실패하는 횟수입니다. 미보고 한계와 별도로 판정합니다.")]
    [SerializeField, Min(1)] private int maxWrongReports = 3;

    [Header("Sound Events")]
    [SerializeField] private UnityEngine.Events.UnityEvent onDayFailed;

    public DayRuntimeState State { get; private set; } = DayRuntimeState.NotStarted;
    public float ElapsedSec { get; private set; }
    public float RemainingSec => Mathf.Max(0f, DurationSec - ElapsedSec);
    public int SuccessReportCount { get; private set; }
    public int WrongReportCount { get; private set; }
    /// <summary>실제 미보고 이상현상 누적 횟수입니다. 접근 단계와 미보고 실패에만 사용합니다.</summary>
    public int MissedAnomalyCount { get; private set; }
    public DayFailureReason FailureReason { get; private set; }
    public DayDefinition CurrentDayDefinition => dayDefinition;
    public float DurationSec => dayDefinition != null ? Mathf.Max(0.01f, dayDefinition.DurationSec) : 600f;

    public System.Action DayStarted;
    public System.Action DayCleared;
    public System.Action DayFailed;
    public System.Action<DayFailureReason> TerminalFailureStarted;
    public System.Action<AnomalyRuntime> MissedAnomalyRegistered;

    private bool terminalFailureStarted;
    private bool loggedEmergencyCompletionHold;
    private Day1FlowController dayFlowController;
    private int MaxMissed => dayDefinition != null ? Mathf.Max(0, dayDefinition.MaxMissed) : 3;
    public int MaxWrongReports => Mathf.Max(1, maxWrongReports);

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        SubscribeEvents();

        if (autoStartOnStart)
            StartDay();
    }

    private void Update()
    {
        if (State != DayRuntimeState.Running)
            return;

        ElapsedSec = Mathf.Min(DurationSec, ElapsedSec + Time.deltaTime);
        NotifyTimeChanged();

        if (ElapsedSec >= DurationSec)
        {
            if (CanCompleteTimedDay())
            {
                ClearDay();
            }
            else if (!loggedEmergencyCompletionHold)
            {
                loggedEmergencyCompletionHold = true;
                Debug.Log("[DayRuntimeController] Day completion is waiting for the required field event.");
            }
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void StartDay()
    {
        // GameManager survives scene changes. A retry must never inherit the
        // terminal failure input lock from the previous day.
        GameManager.Instance?.SetReportInputEnabled(true);

        ElapsedSec = 0f;
        SuccessReportCount = 0;
        WrongReportCount = 0;
        MissedAnomalyCount = 0;
        FailureReason = DayFailureReason.None;
        terminalFailureStarted = false;
        loggedEmergencyCompletionHold = false;
        State = DayRuntimeState.Running;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(false);

        NotifyTimeChanged();
        NotifyWrongReportCountChanged();
        DayStarted?.Invoke();
        Debug.Log($"[DayRuntimeController] Day started. day={GetDayNumber()}, duration={DurationSec}");
    }

    public void PauseDay()
    {
        if (State != DayRuntimeState.Running)
            return;

        State = DayRuntimeState.Paused;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);

        Debug.Log("[DayRuntimeController] Day paused.");
    }

    public void ResumeDay()
    {
        if (State != DayRuntimeState.Paused)
            return;

        State = DayRuntimeState.Running;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(false);

        Debug.Log("[DayRuntimeController] Day resumed.");
    }

    public void ClearDay()
    {
        if (State == DayRuntimeState.Cleared || State == DayRuntimeState.Failed)
            return;

        State = DayRuntimeState.Cleared;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);

        NotifyTimeChanged();
        DayCleared?.Invoke();
        Debug.Log($"[DayRuntimeController] Day cleared. successReports={SuccessReportCount}, wrongReports={WrongReportCount}, missed={MissedAnomalyCount}");
    }

    public void FailDay(DayFailureReason reason = DayFailureReason.None)
    {
        if (State == DayRuntimeState.Cleared || State == DayRuntimeState.Failed)
            return;

        State = DayRuntimeState.Failed;
        if (reason != DayFailureReason.None || FailureReason == DayFailureReason.None)
            FailureReason = reason;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);

        SoundManager.Instance?.PlayFailBgm();
        onDayFailed?.Invoke();
        DayFailed?.Invoke();
        Debug.Log($"[DayRuntimeController] Day failed. wrongReports={WrongReportCount}/{MaxWrongReports}, missed={MissedAnomalyCount}/{MaxMissed}");
    }

    public void RegisterCorrectReport()
    {
        if (State != DayRuntimeState.Running)
            return;

        SuccessReportCount++;
        Debug.Log($"[DayRuntimeController] Correct report. successReports={SuccessReportCount}");
    }

    public bool RegisterWrongReport()
    {
        if (State != DayRuntimeState.Running)
            return false;

        WrongReportCount++;
        NotifyWrongReportCountChanged();
        Debug.Log($"[DayRuntimeController] Wrong report. wrongReports={WrongReportCount}, maxWrongReports={MaxWrongReports}");
        bool reachedLimit = WrongReportCount >= MaxWrongReports;
        if (reachedLimit)
            BeginTerminalFailure(DayFailureReason.WrongReports);

        return reachedLimit;
    }

    public void RegisterMissedAnomaly(AnomalyRuntime runtime)
    {
        if (State != DayRuntimeState.Running)
            return;

        MissedAnomalyCount++;
        bool reachedLimit = MaxMissed > 0 && MissedAnomalyCount >= MaxMissed;
        if (reachedLimit)
            BeginTerminalFailure(DayFailureReason.MissedAnomalies);

        MissedAnomalyRegistered?.Invoke(runtime);
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayMissedAnomaly(runtime);

        string anomalyId = runtime != null && runtime.Definition != null ? runtime.Definition.AnomalyId : "Unknown";
        Debug.Log($"[DayRuntimeController] Missed anomaly counted. anomaly={anomalyId}, missed={MissedAnomalyCount}, maxMissed={MaxMissed}");
        if (reachedLimit)
            FailDay(DayFailureReason.MissedAnomalies);
    }

    private void BeginTerminalFailure(DayFailureReason reason)
    {
        if (terminalFailureStarted)
            return;

        terminalFailureStarted = true;
        FailureReason = reason;
        TerminalFailureStarted?.Invoke(reason);
    }

    private void NotifyTimeChanged()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayTimeChanged(ElapsedSec, DurationSec);
    }

    private void NotifyWrongReportCountChanged()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayWrongReportCountChanged(WrongReportCount, MaxWrongReports);
    }

    private void SubscribeEvents()
    {
        ResolveReferences();

        if (anomalyService == null)
            return;

        anomalyService.AnomalyMissed -= RegisterMissedAnomaly;
        anomalyService.AnomalyMissed += RegisterMissedAnomaly;
    }

    private void UnsubscribeEvents()
    {
        if (anomalyService != null)
            anomalyService.AnomalyMissed -= RegisterMissedAnomaly;
    }

    private void ResolveReferences()
    {
        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();

        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);

        if (dayDefinition == null)
        {
            CCTVTestSceneController sceneController = FindObjectOfType<CCTVTestSceneController>();
            if (sceneController != null)
                dayDefinition = sceneController.CurrentDayDefinition;
        }
    }

    private bool CanCompleteTimedDay()
    {
        if (dayDefinition == null || !dayDefinition.EnableEmergencyDispatch)
            return true;

        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);

        // 기존 단독 테스트 씬처럼 FlowController가 없는 환경은 이전 종료 동작을 유지한다.
        return dayFlowController == null || dayFlowController.CanCompleteTimedDay;
    }

    private int GetDayNumber()
    {
        return dayDefinition != null ? dayDefinition.Day : 0;
    }
}
