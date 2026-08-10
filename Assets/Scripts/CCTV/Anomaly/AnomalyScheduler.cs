using System.Collections.Generic;
using UnityEngine;

public class AnomalyScheduler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private AnomalyService anomalyService;

    [Header("Retry")]
    [SerializeField, Min(0.1f)] private float blockedRetryDelaySec = 1f;

    [Header("Random Variety")]
    [Tooltip("랜덤 풀에서 직전에 나온 이상현상은 다음 추첨에서 제외합니다. 다른 후보가 없을 때만 예외로 허용합니다.")]
    [SerializeField] private bool preventConsecutiveRandomAnomaly = true;

    [Header("Observation Cycle")]
    [Tooltip("활성/연출 중인 이상현상이 있으면 다른 구역을 포함해 다음 이상현상을 만들지 않습니다.")]
    [SerializeField] private bool allowOnlyOneActiveAnomaly = true;
    [Tooltip("정상 보고 뒤 기준 화면을 확인할 수 있도록 주는 안정 시간입니다.")]
    [SerializeField, Min(0f)] private float correctReportStableDurationSec = 2f;
    [Tooltip("미보고 신호 유실과 배경 변경 뒤 기준 화면을 확인할 수 있도록 주는 안정 시간입니다.")]
    [SerializeField, Min(0f)] private float missedReportStableDurationSec = 4f;

    [Header("Runtime")]
    [SerializeField] private bool generationPaused;

    private readonly HashSet<int> triggeredFixedScheduleIndexes = new HashSet<int>();
    private readonly HashSet<AnomalyDefinition> usedNonRepeatRandomAnomalies = new HashSet<AnomalyDefinition>();

    private DayDefinition currentDayDefinition;
    private float randomRemainingSec;
    private bool awaitingObservationCycleResult;
    private float postCycleStableRemainingSec;
    private AnomalyDefinition lastRandomAnomaly;

    public bool GenerationPaused => generationPaused;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SubscribeDayEvents();
    }

    private void Start()
    {
        ResolveReferences();

        if (dayRuntimeController != null && dayRuntimeController.State == DayRuntimeState.Running)
            ResetSchedule(dayRuntimeController.CurrentDayDefinition);
    }

    private void Update()
    {
        if (generationPaused)
            return;

        if (dayRuntimeController == null || anomalyService == null)
            return;

        if (dayRuntimeController.State != DayRuntimeState.Running)
            return;

        if (currentDayDefinition == null)
            ResetSchedule(dayRuntimeController.CurrentDayDefinition);

        if (currentDayDefinition == null)
            return;

        if (awaitingObservationCycleResult || HasActiveAnomalies())
            return;

        if (postCycleStableRemainingSec > 0f)
        {
            postCycleStableRemainingSec = Mathf.Max(0f, postCycleStableRemainingSec - Time.deltaTime);
            if (postCycleStableRemainingSec <= 0f)
                ResetRandomTimer();

            return;
        }

        TickFixedSchedule();
        if (awaitingObservationCycleResult || HasActiveAnomalies())
            return;

        // 도입용 고정 이상현상이 있는 날에는, 그 첫 항목이 발동되기 전 랜덤이 앞서지 않는다.
        if (currentDayDefinition.WaitForFirstFixedAnomalyBeforeRandom && !HasFirstFixedScheduleTriggered())
            return;

        TickRandomSchedule(Time.deltaTime);
    }

    private void OnDisable()
    {
        UnsubscribeDayEvents();
    }

    public void SetGenerationPaused(bool paused)
    {
        generationPaused = paused;
    }

    /// <summary>
    /// 현재 랜덤 풀의 규칙(가중치, 연속 중복 방지, 단일 활성 제한)을 그대로 사용해
    /// 첫 이상현상을 즉시 시작합니다. Day 1 조작 안내 완료 시점에 사용합니다.
    /// </summary>
    public bool TryTriggerRandomAnomalyNow()
    {
        ResolveReferences();

        if (dayRuntimeController == null || anomalyService == null ||
            dayRuntimeController.State != DayRuntimeState.Running)
            return false;

        if (currentDayDefinition == null)
            ResetSchedule(dayRuntimeController.CurrentDayDefinition);

        if (currentDayDefinition == null || !currentDayDefinition.EnableRandomSchedule ||
            awaitingObservationCycleResult || HasActiveAnomalies())
            return false;

        AnomalyDefinition selectedAnomaly = SelectRandomAnomaly();
        if (selectedAnomaly == null || !TryActivateScheduledAnomaly(selectedAnomaly))
            return false;

        MarkRandomAnomalyUsed(selectedAnomaly);
        lastRandomAnomaly = selectedAnomaly;
        return true;
    }

    private void ResetSchedule(DayDefinition dayDefinition)
    {
        currentDayDefinition = dayDefinition;
        triggeredFixedScheduleIndexes.Clear();
        usedNonRepeatRandomAnomalies.Clear();
        lastRandomAnomaly = null;
        awaitingObservationCycleResult = false;
        postCycleStableRemainingSec = 0f;
        ResetRandomTimer();
    }

    private void TickFixedSchedule()
    {
        IReadOnlyList<FixedAnomalyScheduleEntry> fixedSchedule = currentDayDefinition.FixedSchedule;
        if (fixedSchedule == null)
            return;

        for (int i = 0; i < fixedSchedule.Count; i++)
        {
            FixedAnomalyScheduleEntry entry = fixedSchedule[i];
            if (entry == null || entry.Anomaly == null)
                continue;

            if (entry.TriggerOnce && triggeredFixedScheduleIndexes.Contains(i))
                continue;

            if (dayRuntimeController.ElapsedSec < entry.TriggerElapsedSec)
                continue;

            if (TryActivateScheduledAnomaly(entry.Anomaly))
            {
                triggeredFixedScheduleIndexes.Add(i);
                // 고정 도입 이상현상도 직전 출현 항목으로 기억해,
                // 바로 다음 랜덤 추첨에서 같은 항목이 연속되지 않게 한다.
                if (preventConsecutiveRandomAnomaly)
                    lastRandomAnomaly = entry.Anomaly;
                return;
            }
        }
    }

    private bool HasFirstFixedScheduleTriggered()
    {
        IReadOnlyList<FixedAnomalyScheduleEntry> fixedSchedule = currentDayDefinition.FixedSchedule;
        if (fixedSchedule == null || fixedSchedule.Count == 0)
            return true;

        for (int i = 0; i < fixedSchedule.Count; i++)
        {
            FixedAnomalyScheduleEntry entry = fixedSchedule[i];
            if (entry != null && entry.Anomaly != null)
                return triggeredFixedScheduleIndexes.Contains(i);
        }

        return true;
    }

    private void TickRandomSchedule(float deltaTime)
    {
        if (!currentDayDefinition.EnableRandomSchedule)
            return;

        randomRemainingSec -= deltaTime;
        if (randomRemainingSec > 0f)
            return;

        AnomalyDefinition selectedAnomaly = SelectRandomAnomaly();
        if (selectedAnomaly == null)
        {
            ResetRandomTimer();
            return;
        }

        if (TryActivateScheduledAnomaly(selectedAnomaly))
        {
            MarkRandomAnomalyUsed(selectedAnomaly);
            lastRandomAnomaly = selectedAnomaly;
        }
        else
        {
            randomRemainingSec = blockedRetryDelaySec;
        }
    }

    private bool TryActivateScheduledAnomaly(AnomalyDefinition anomaly)
    {
        if (anomaly == null)
            return false;

        if (IsAreaOccupied(anomaly.AreaId))
            return false;

        AnomalyRuntime runtime = anomalyService.Activate(anomaly);
        bool activated = runtime != null;
        if (activated)
        {
            awaitingObservationCycleResult = true;
            Debug.Log($"[AnomalyScheduler] Activated scheduled anomaly={anomaly.AnomalyId}, area={anomaly.AreaId}");
        }

        return activated;
    }

    private bool HasActiveAnomalies()
    {
        if (!allowOnlyOneActiveAnomaly || anomalyService == null)
            return false;

        IReadOnlyList<AnomalyRuntime> activeAnomalies = anomalyService.ActiveAnomalies;
        return activeAnomalies != null && activeAnomalies.Count > 0;
    }

    private bool IsAreaOccupied(AreaId areaId)
    {
        IReadOnlyList<AnomalyRuntime> activeAnomalies = anomalyService.ActiveAnomalies;
        if (activeAnomalies == null)
            return false;

        for (int i = 0; i < activeAnomalies.Count; i++)
        {
            AnomalyRuntime runtime = activeAnomalies[i];
            if (runtime == null || runtime.Definition == null)
                continue;

            if (runtime.Definition.AreaId != areaId)
                continue;

            if (runtime.State == AnomalyState.Activating || runtime.State == AnomalyState.Active)
                return true;
        }

        return false;
    }

    private AnomalyDefinition SelectRandomAnomaly()
    {
        IReadOnlyList<RandomAnomalyPoolEntry> pool = currentDayDefinition.RandomPool;
        if (pool == null)
            return null;

        bool excludeLastAnomaly = preventConsecutiveRandomAnomaly && lastRandomAnomaly != null;
        int totalWeight = GetRandomCandidateWeight(pool, excludeLastAnomaly);

        // 풀에 다른 후보가 하나도 없으면 스케줄이 멈추지 않도록 직전 항목을 예외로 허용한다.
        if (totalWeight <= 0 && excludeLastAnomaly)
        {
            excludeLastAnomaly = false;
            totalWeight = GetRandomCandidateWeight(pool, false);
        }

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            RandomAnomalyPoolEntry entry = pool[i];
            if (!IsRandomEntryAvailable(entry) || (excludeLastAnomaly && entry.Anomaly == lastRandomAnomaly))
                continue;

            if (roll < entry.Weight)
                return entry.Anomaly;

            roll -= entry.Weight;
        }

        return null;
    }

    private int GetRandomCandidateWeight(IReadOnlyList<RandomAnomalyPoolEntry> pool, bool excludeLastAnomaly)
    {
        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            RandomAnomalyPoolEntry entry = pool[i];
            if (!IsRandomEntryAvailable(entry) || (excludeLastAnomaly && entry.Anomaly == lastRandomAnomaly))
                continue;

            totalWeight += entry.Weight;
        }

        return totalWeight;
    }

    private bool IsRandomEntryAvailable(RandomAnomalyPoolEntry entry)
    {
        if (entry == null || entry.Anomaly == null)
            return false;

        return entry.AllowRepeat || !usedNonRepeatRandomAnomalies.Contains(entry.Anomaly);
    }

    private void MarkRandomAnomalyUsed(AnomalyDefinition anomaly)
    {
        IReadOnlyList<RandomAnomalyPoolEntry> pool = currentDayDefinition.RandomPool;
        if (pool == null)
            return;

        for (int i = 0; i < pool.Count; i++)
        {
            RandomAnomalyPoolEntry entry = pool[i];
            if (entry == null || entry.Anomaly != anomaly || entry.AllowRepeat)
                continue;

            usedNonRepeatRandomAnomalies.Add(anomaly);
            return;
        }
    }

    private void ResetRandomTimer()
    {
        if (currentDayDefinition == null)
        {
            randomRemainingSec = 0f;
            return;
        }

        randomRemainingSec = Random.Range(
            currentDayDefinition.RandomMinIntervalSec,
            currentDayDefinition.RandomMaxIntervalSec
        );
    }

    private void SubscribeDayEvents()
    {
        ResolveReferences();

        if (dayRuntimeController == null)
            return;

        dayRuntimeController.DayStarted -= HandleDayStarted;
        dayRuntimeController.DayCleared -= HandleDayEnded;
        dayRuntimeController.DayFailed -= HandleDayEnded;
        dayRuntimeController.DayStarted += HandleDayStarted;
        dayRuntimeController.DayCleared += HandleDayEnded;
        dayRuntimeController.DayFailed += HandleDayEnded;

        if (anomalyService != null)
        {
            anomalyService.AnomalyResolved -= HandleScheduledAnomalyResolved;
            anomalyService.AnomalyMissed -= HandleScheduledAnomalyMissed;
            anomalyService.AnomalyResolved += HandleScheduledAnomalyResolved;
            anomalyService.AnomalyMissed += HandleScheduledAnomalyMissed;
        }
    }

    private void UnsubscribeDayEvents()
    {
        if (dayRuntimeController == null)
            return;

        dayRuntimeController.DayStarted -= HandleDayStarted;
        dayRuntimeController.DayCleared -= HandleDayEnded;
        dayRuntimeController.DayFailed -= HandleDayEnded;

        if (anomalyService != null)
        {
            anomalyService.AnomalyResolved -= HandleScheduledAnomalyResolved;
            anomalyService.AnomalyMissed -= HandleScheduledAnomalyMissed;
        }
    }

    private void HandleDayStarted()
    {
        ResetSchedule(dayRuntimeController != null ? dayRuntimeController.CurrentDayDefinition : null);
    }

    private void HandleDayEnded()
    {
        currentDayDefinition = null;
        triggeredFixedScheduleIndexes.Clear();
        usedNonRepeatRandomAnomalies.Clear();
        lastRandomAnomaly = null;
        randomRemainingSec = 0f;
        awaitingObservationCycleResult = false;
        postCycleStableRemainingSec = 0f;
    }

    private void HandleScheduledAnomalyResolved(AnomalyRuntime runtime)
    {
        BeginPostCycleStableTime(correctReportStableDurationSec, "resolved", runtime);
    }

    private void HandleScheduledAnomalyMissed(AnomalyRuntime runtime)
    {
        BeginPostCycleStableTime(missedReportStableDurationSec, "missed", runtime);
    }

    private void BeginPostCycleStableTime(float durationSec, string result, AnomalyRuntime runtime)
    {
        // 튜토리얼처럼 Scheduler가 직접 시작하지 않은 이상현상은 여기서 다음 랜덤 타이머를 건드리지 않는다.
        if (!awaitingObservationCycleResult)
            return;

        awaitingObservationCycleResult = false;
        postCycleStableRemainingSec = Mathf.Max(0f, durationSec);

        string anomalyId = runtime != null && runtime.Definition != null ? runtime.Definition.AnomalyId : "Unknown";
        Debug.Log($"[AnomalyScheduler] Observation cycle {result}. anomaly={anomalyId}, stable={postCycleStableRemainingSec:F1}s");

        if (postCycleStableRemainingSec <= 0f)
            ResetRandomTimer();
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();
    }
}
