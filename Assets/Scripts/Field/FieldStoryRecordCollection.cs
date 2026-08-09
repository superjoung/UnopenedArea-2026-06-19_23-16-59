using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Day 2의 현장 기록물 조사를 집계합니다.
/// 각 FieldStoryRecordInspect는 이 컴포넌트의 자식으로 두면 자동 등록되며,
/// 마지막 기록물을 닫았을 때만 현장 목표를 완료합니다.
/// </summary>
public class FieldStoryRecordCollection : MonoBehaviour
{
    [Header("Records")]
    [Tooltip("비워 두면 이 오브젝트의 자식에서 FieldStoryRecordInspect를 전부 찾아 사용합니다.")]
    [SerializeField] private FieldStoryRecordInspect[] records;
    [SerializeField, Min(1)] private int requiredRecordCount = 6;

    [Header("Completion")]
    [SerializeField] private string completionStoryFlag = "D2_LOG_O06_READ";
    [SerializeField] private Day1FlowController dayFlowController;

    [Header("Shared Popup")]
    [Tooltip("기록물 전체가 함께 쓰는 확대 팝업입니다.")]
    [SerializeField] private FieldStoryRecordPopup sharedPopup;

    private readonly HashSet<FieldStoryRecordInspect> inspectedRecords = new HashSet<FieldStoryRecordInspect>();

    public int InspectedCount => inspectedRecords.Count;
    public int RequiredRecordCount => Mathf.Min(Mathf.Max(1, requiredRecordCount), records != null ? records.Length : 1);
    public bool IsCompleted { get; private set; }

    public event Action<int, int> ProgressChanged;
    public event Action Completed;

    public void ShowRecord(string title, string body)
    {
        ResolveReferences();
        sharedPopup?.Show(title, body);
    }

    public void HideRecord()
    {
        sharedPopup?.Hide();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    /// <summary>각 기록물의 확인 완료 시 자동 호출됩니다.</summary>
    public void RegisterRecordInspected(FieldStoryRecordInspect record)
    {
        if (record == null || IsCompleted || !inspectedRecords.Add(record))
            return;

        ProgressChanged?.Invoke(InspectedCount, RequiredRecordCount);
        Debug.Log($"[FieldStoryRecordCollection] Record inspected. {InspectedCount}/{RequiredRecordCount}");

        if (InspectedCount < RequiredRecordCount)
            return;

        IsCompleted = true;
        StoryFlagStore.Set(completionStoryFlag);
        Completed?.Invoke();

        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);

        if (dayFlowController != null &&
            dayFlowController.CurrentEmergencyObjectiveType == EmergencyObjectiveType.StoryRecordInspection)
        {
            FieldModeController fieldModeController = FindFirstObjectByType<FieldModeController>(FindObjectsInactive.Include);
            fieldModeController?.PlayPowerRestoreLightEffect();
            SoundManager.Instance?.PlayBreakerPowerOnSfx();
        }

        dayFlowController?.CompleteEmergencyObjective();
        Debug.Log($"[FieldStoryRecordCollection] Collection completed. flag={completionStoryFlag}");
    }

    private void ResolveReferences()
    {
        if (records == null || records.Length == 0)
            records = GetComponentsInChildren<FieldStoryRecordInspect>(true);

        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);

        if (sharedPopup == null)
            sharedPopup = FindFirstObjectByType<FieldStoryRecordPopup>(FindObjectsInactive.Include);
    }
}
