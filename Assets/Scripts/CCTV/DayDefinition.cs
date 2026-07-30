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

    [Header("Tutorial Flow")]
    [SerializeField] private AnomalyDefinition tutorialAnomaly;
    [SerializeField, Min(0)] private int tutorialRequiredChannelSwitches = 3;
    [SerializeField, Min(0f)] private float tutorialFallbackElapsedSec = 60f;

    [Header("Emergency Dispatch")]
    [SerializeField] private bool enableEmergencyDispatch;
    [SerializeField, Min(0)] private int emergencyRequiredCorrectReports = 3;
    [SerializeField, Range(0f, 1f)] private float emergencyTriggerProgress = 0.7f;

    [Header("Missed Escalation Channels")]
    [Tooltip("미보고 누적 시 제어실 외부 해금 및 CCTVRoom 탈취를 사용합니다. Day 1 기존 설정은 호환을 위해 항상 켜집니다.")]
    [SerializeField] private bool enableMissedEscalationChannels;

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
    public AnomalyDefinition TutorialAnomaly => tutorialAnomaly;
    public int TutorialRequiredChannelSwitches => Mathf.Max(0, tutorialRequiredChannelSwitches);
    public float TutorialFallbackElapsedSec => Mathf.Max(0f, tutorialFallbackElapsedSec);
    public bool EnableEmergencyDispatch => enableEmergencyDispatch;
    public int EmergencyRequiredCorrectReports => Mathf.Max(0, emergencyRequiredCorrectReports);
    public float EmergencyTriggerProgress => Mathf.Clamp01(emergencyTriggerProgress);
    public bool EnableMissedEscalationChannels => day == 1 || enableMissedEscalationChannels;
    public IReadOnlyList<FixedAnomalyScheduleEntry> FixedSchedule => fixedSchedule;
    public IReadOnlyList<RandomAnomalyPoolEntry> RandomPool => randomPool;
    public bool EnableRandomSchedule => enableRandomSchedule;
    public float RandomMinIntervalSec => Mathf.Max(0f, randomMinIntervalSec);
    public float RandomMaxIntervalSec => Mathf.Max(RandomMinIntervalSec, randomMaxIntervalSec);
}
