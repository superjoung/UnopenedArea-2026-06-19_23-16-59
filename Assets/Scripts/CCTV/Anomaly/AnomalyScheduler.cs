using System.Collections.Generic;
using UnityEngine;

public class AnomalyScheduler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private AnomalyService anomalyService;

    [Header("Retry")]
    [SerializeField, Min(0.1f)] private float blockedRetryDelaySec = 1f;

    [Header("Runtime")]
    [SerializeField] private bool generationPaused;

    private readonly HashSet<int> triggeredFixedScheduleIndexes = new HashSet<int>();
    private readonly HashSet<AnomalyDefinition> usedNonRepeatRandomAnomalies = new HashSet<AnomalyDefinition>();

    private DayDefinition currentDayDefinition;
    private float randomRemainingSec;

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

        TickFixedSchedule();
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

    private void ResetSchedule(DayDefinition dayDefinition)
    {
        currentDayDefinition = dayDefinition;
        triggeredFixedScheduleIndexes.Clear();
        usedNonRepeatRandomAnomalies.Clear();
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
                triggeredFixedScheduleIndexes.Add(i);
        }
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
            ResetRandomTimer();
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
            Debug.Log($"[AnomalyScheduler] Activated scheduled anomaly={anomaly.AnomalyId}, area={anomaly.AreaId}");

        return activated;
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

        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            RandomAnomalyPoolEntry entry = pool[i];
            if (!IsRandomEntryAvailable(entry))
                continue;

            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            RandomAnomalyPoolEntry entry = pool[i];
            if (!IsRandomEntryAvailable(entry))
                continue;

            if (roll < entry.Weight)
                return entry.Anomaly;

            roll -= entry.Weight;
        }

        return null;
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
    }

    private void UnsubscribeDayEvents()
    {
        if (dayRuntimeController == null)
            return;

        dayRuntimeController.DayStarted -= HandleDayStarted;
        dayRuntimeController.DayCleared -= HandleDayEnded;
        dayRuntimeController.DayFailed -= HandleDayEnded;
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
        randomRemainingSec = 0f;
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();
    }
}
