using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class FixedAnomalyScheduleEntry
{
    [SerializeField, Min(0f)] private float triggerElapsedSec;
    [SerializeField] private AnomalyDefinition anomaly;
    [SerializeField] private bool triggerOnce = true;

    public float TriggerElapsedSec => Mathf.Max(0f, triggerElapsedSec);
    public AnomalyDefinition Anomaly => anomaly;
    public bool TriggerOnce => triggerOnce;
}

[System.Serializable]
public sealed class RandomAnomalyPoolEntry
{
    [SerializeField] private AnomalyDefinition anomaly;
    [SerializeField, Min(1)] private int weight = 1;
    [SerializeField] private bool allowRepeat = true;

    public AnomalyDefinition Anomaly => anomaly;
    public int Weight => Mathf.Max(1, weight);
    public bool AllowRepeat => allowRepeat;
}

[CreateAssetMenu(fileName = "DayDefinition", menuName = "Unrecorded Area/Day Definition")]
public class DayDefinition : ScriptableObject
{
    [Header("Day")]
    [SerializeField] private int day = 1;
    [SerializeField] private float durationSec = 600f;
    [SerializeField] private int maxMissed = 3;

    [Header("CCTV Areas")]
    [SerializeField] private CCTVAreaDefinition[] activeAreas;

    [Header("Fixed Anomaly Schedule")]
    [SerializeField] private FixedAnomalyScheduleEntry[] fixedSchedule;

    [Header("Random Anomaly Schedule")]
    [SerializeField] private bool enableRandomSchedule;
    [SerializeField, Min(0f)] private float randomMinIntervalSec = 30f;
    [SerializeField, Min(0f)] private float randomMaxIntervalSec = 60f;
    [SerializeField] private RandomAnomalyPoolEntry[] randomPool;

    public int Day => day;
    public float DurationSec => durationSec;
    public int MaxMissed => maxMissed;
    public IReadOnlyList<CCTVAreaDefinition> ActiveAreas => activeAreas;
    public IReadOnlyList<FixedAnomalyScheduleEntry> FixedSchedule => fixedSchedule;
    public IReadOnlyList<RandomAnomalyPoolEntry> RandomPool => randomPool;
    public bool EnableRandomSchedule => enableRandomSchedule;
    public float RandomMinIntervalSec => Mathf.Max(0f, randomMinIntervalSec);
    public float RandomMaxIntervalSec => Mathf.Max(RandomMinIntervalSec, randomMaxIntervalSec);
}
