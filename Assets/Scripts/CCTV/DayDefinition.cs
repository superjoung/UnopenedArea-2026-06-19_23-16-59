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

/// <summary>
/// 긴급 현장 전환 뒤에 플레이어가 해결해야 하는 목표의 종류입니다.
/// 상태 전환 자체는 공통으로 유지하고, 실제 상호작용만 일차별로 바꿉니다.
/// </summary>
public enum EmergencyObjectiveType
{
    PowerRestore = 0,
    StoryRecordInspection = 1,
    ServerReboot = 2,
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
    [Tooltip("켜면 정답 횟수와 진행률을 모두 충족해야 합니다. 끄면 둘 중 하나를 먼저 충족했을 때 현장 이벤트를 시작합니다.")]
    [SerializeField] private bool emergencyRequiresBothConditions = true;

    [Header("Emergency Objective")]
    [SerializeField] private EmergencyObjectiveType emergencyObjectiveType = EmergencyObjectiveType.PowerRestore;
    [Tooltip("Leave empty to use the default dispatch message for the objective type.")]
    [SerializeField, TextArea(2, 3)] private string emergencyDispatchText;
    [Tooltip("비워두면 목표 유형에 맞는 기본 안내 문구를 사용합니다.")]
    [SerializeField, TextArea(2, 3)] private string emergencyFieldObjectiveText;
    [Tooltip("비워두면 목표 유형에 맞는 기본 완료 문구를 사용합니다.")]
    [SerializeField, TextArea(2, 3)] private string emergencyRecoveryText;

    [Header("Phone Dialogue")]
    [Tooltip("비워두면 CommonRoot의 기본 대사를 사용합니다. 일차 전용 대사는 이 에셋에 저장합니다.")]
    [SerializeField] private StoryDialogueLine[] phoneDialogue;

    [Header("Missed Escalation Channels")]
    [Tooltip("미보고 누적 시 제어실 외부 해금 및 CCTVRoom 탈취를 사용합니다. Day 1 기존 설정은 호환을 위해 항상 켜집니다.")]
    [SerializeField] private bool enableMissedEscalationChannels;

    [Header("Fixed Anomaly Schedule")]
    [SerializeField] private FixedAnomalyScheduleEntry[] fixedSchedule;

    [Header("Random Anomaly Schedule")]
    [SerializeField] private bool enableRandomSchedule;
    [Tooltip("첫 번째 Fixed Schedule 항목이 발동될 때까지 랜덤 이상현상 추첨을 보류합니다. 도입용 고정 이상현상이 필요한 일차에 사용합니다.")]
    [SerializeField] private bool waitForFirstFixedAnomalyBeforeRandom;
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
    public bool EmergencyRequiresBothConditions => emergencyRequiresBothConditions;
    public EmergencyObjectiveType EmergencyObjectiveType => emergencyObjectiveType;
    public string EmergencyDispatchText => emergencyDispatchText;
    public string EmergencyFieldObjectiveText => emergencyFieldObjectiveText;
    public string EmergencyRecoveryText => emergencyRecoveryText;
    public IReadOnlyList<StoryDialogueLine> PhoneDialogue => phoneDialogue;
    public bool EnableMissedEscalationChannels => day == 1 || enableMissedEscalationChannels;
    public IReadOnlyList<FixedAnomalyScheduleEntry> FixedSchedule => fixedSchedule;
    public IReadOnlyList<RandomAnomalyPoolEntry> RandomPool => randomPool;
    public bool EnableRandomSchedule => enableRandomSchedule;
    public bool WaitForFirstFixedAnomalyBeforeRandom => waitForFirstFixedAnomalyBeforeRandom;
    public float RandomMinIntervalSec => Mathf.Max(0f, randomMinIntervalSec);
    public float RandomMaxIntervalSec => Mathf.Max(RandomMinIntervalSec, randomMaxIntervalSec);
}
