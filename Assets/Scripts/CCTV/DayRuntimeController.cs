using UnityEngine;

public enum DayRuntimeState
{
    NotStarted = 0,
    Running = 1,
    Paused = 2,
    Cleared = 3,
    Failed = 4,
}

public class DayRuntimeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayDefinition dayDefinition;
    [SerializeField] private AnomalyService anomalyService;

    [Header("Options")]
    [SerializeField] private bool autoStartOnStart = true;

    public DayRuntimeState State { get; private set; } = DayRuntimeState.NotStarted;
    public float ElapsedSec { get; private set; }
    public float RemainingSec => Mathf.Max(0f, DurationSec - ElapsedSec);
    public int SuccessReportCount { get; private set; }
    public int WrongOrMissedCount { get; private set; }
    public DayDefinition CurrentDayDefinition => dayDefinition;
    public float DurationSec => dayDefinition != null ? Mathf.Max(0.01f, dayDefinition.DurationSec) : 600f;

    public System.Action DayStarted;
    public System.Action DayCleared;
    public System.Action DayFailed;
    public System.Action<AnomalyRuntime> MissedAnomalyRegistered;

    private int MaxMissed => dayDefinition != null ? Mathf.Max(0, dayDefinition.MaxMissed) : 3;

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
            ClearDay();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void StartDay()
    {
        ElapsedSec = 0f;
        SuccessReportCount = 0;
        WrongOrMissedCount = 0;
        State = DayRuntimeState.Running;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(false);

        NotifyTimeChanged();
        NotifyFailureCountChanged();
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
        Debug.Log($"[DayRuntimeController] Day cleared. successReports={SuccessReportCount}, wrongOrMissed={WrongOrMissedCount}");
    }

    public void FailDay()
    {
        if (State == DayRuntimeState.Cleared || State == DayRuntimeState.Failed)
            return;

        State = DayRuntimeState.Failed;

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);

        DayFailed?.Invoke();
        Debug.Log($"[DayRuntimeController] Day failed. wrongOrMissed={WrongOrMissedCount}, maxMissed={MaxMissed}");
    }

    public void RegisterCorrectReport()
    {
        if (State != DayRuntimeState.Running)
            return;

        SuccessReportCount++;
        Debug.Log($"[DayRuntimeController] Correct report. successReports={SuccessReportCount}");
    }

    public void RegisterWrongReport()
    {
        if (State != DayRuntimeState.Running)
            return;

        WrongOrMissedCount++;
        NotifyFailureCountChanged();
        Debug.Log($"[DayRuntimeController] Wrong report. wrongOrMissed={WrongOrMissedCount}, maxMissed={MaxMissed}");
        CheckFailByWrongOrMissedCount();
    }

    public void RegisterMissedAnomaly(AnomalyRuntime runtime)
    {
        if (State != DayRuntimeState.Running)
            return;

        WrongOrMissedCount++;
        NotifyFailureCountChanged();
        MissedAnomalyRegistered?.Invoke(runtime);
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayMissedAnomaly(runtime);

        string anomalyId = runtime != null && runtime.Definition != null ? runtime.Definition.AnomalyId : "Unknown";
        Debug.Log($"[DayRuntimeController] Missed anomaly counted. anomaly={anomalyId}, wrongOrMissed={WrongOrMissedCount}, maxMissed={MaxMissed}");
        CheckFailByWrongOrMissedCount();
    }

    private void CheckFailByWrongOrMissedCount()
    {
        if (MaxMissed > 0 && WrongOrMissedCount >= MaxMissed)
            FailDay();
    }

    private void NotifyTimeChanged()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayTimeChanged(ElapsedSec, DurationSec);
    }

    private void NotifyFailureCountChanged()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyDayFailureCountChanged(WrongOrMissedCount, MaxMissed);
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

        if (dayDefinition == null)
        {
            CCTVTestSceneController sceneController = FindObjectOfType<CCTVTestSceneController>();
            if (sceneController != null)
                dayDefinition = sceneController.CurrentDayDefinition;
        }
    }

    private int GetDayNumber()
    {
        return dayDefinition != null ? dayDefinition.Day : 0;
    }
}
