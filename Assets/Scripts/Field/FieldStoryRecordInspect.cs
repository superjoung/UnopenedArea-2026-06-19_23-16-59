using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// D2의 O-06 기록처럼, 현장에 놓인 문서를 조사해 긴급 목표를 완료하는 공통 상호작용입니다.
/// 문서 이미지를 담은 Popup Root는 직접 만들어 할당하고, 이 컴포넌트는 접근/입력/잠금/복귀만 맡습니다.
/// </summary>
public class FieldStoryRecordInspect : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.75f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField, Min(0f)] private float closeUnlockDelay = 1f;
    [Tooltip("조사 완료 후 Day Flow의 현장 목표 완료를 호출합니다.")]
    [SerializeField] private bool completeEmergencyObjectiveOnClose = true;
    [Tooltip("비워두면 플래그를 기록하지 않습니다. Day 2 기록 조사는 D2_LOG_O06_READ를 사용합니다.")]
    [SerializeField] private string completionStoryFlag = "D2_LOG_O06_READ";

    [Header("Record Content")]
    [SerializeField] private string recordTitle;
    [SerializeField, TextArea(3, 8)] private string recordBody;

    [Header("Visuals")]
    [SerializeField] private GameObject outlineVisual;
    [Tooltip("확대 문서 이미지와 암전 패널을 포함하는 UI 루트입니다. 평소에는 비활성 상태여야 합니다.")]
    [SerializeField] private GameObject inspectPopupRoot;
    [Tooltip("공용 확대 팝업을 직접 연결할 때 사용합니다. 수집 루트에 공용 팝업이 있으면 그쪽을 우선 사용합니다.")]
    [SerializeField] private FieldStoryRecordPopup inspectPopup;

    [Header("References (optional)")]
    [SerializeField] private Transform player;
    [SerializeField] private FieldPlayerMovementController playerMovement;
    [SerializeField] private Day1FlowController dayFlowController;
    [SerializeField] private FieldStoryRecordCollection recordCollection;

    public bool IsPlayerInRange { get; private set; }
    public bool IsInspecting { get; private set; }
    public bool IsCompleted { get; private set; }

    public event Action Inspected;

    private float closeUnlockAt;

    private void Awake()
    {
        ResolveReferences();
        ResolveOutlineVisual();
        SetOutlineVisible(false);
        SetPopupVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveOutlineVisual();
        IsInspecting = false;
        SetPopupVisible(false);
        RefreshRange();
    }

    private void Update()
    {
        if (PausePanelController.IsPaused || IsCompleted)
            return;

        if (IsInspecting)
        {
            if (Time.unscaledTime >= closeUnlockAt &&
                (Input.GetKeyDown(interactionKey) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
            {
                CompleteInspection();
            }

            return;
        }

        RefreshRange();
        if (IsPlayerInRange && Input.GetKeyDown(interactionKey))
            BeginInspection();
    }

    public void BeginInspection()
    {
        if (IsCompleted || IsInspecting || !IsPlayerInRange)
            return;

        IsInspecting = true;
        closeUnlockAt = Time.unscaledTime + closeUnlockDelay;
        SetOutlineVisible(false);
        ShowRecordPopup();
        playerMovement?.SetInputEnabled(false);
    }

    /// <summary>UI의 닫기 버튼에도 연결할 수 있습니다. 최초 잠금 시간 중에는 무시됩니다.</summary>
    public void TryCloseInspection()
    {
        if (IsInspecting && Time.unscaledTime >= closeUnlockAt)
            CompleteInspection();
    }

    private void CompleteInspection()
    {
        if (!IsInspecting)
            return;

        IsInspecting = false;
        IsCompleted = true;
        HideRecordPopup();
        playerMovement?.SetInputEnabled(true);
        Inspected?.Invoke();

        if (recordCollection != null)
        {
            recordCollection.RegisterRecordInspected(this);
            return;
        }

        StoryFlagStore.Set(completionStoryFlag);

        if (completeEmergencyObjectiveOnClose)
        {
            if (dayFlowController == null)
                dayFlowController = FindFirstObjectByType<Day1FlowController>();

            // Day 2 기록물은 배전반을 조작해 복구하는 목표가 아니다.
            // 문서를 닫은 뒤 시설이 스스로 정상화되는 듯한 조명 연출만 재생한다.
            if (dayFlowController != null &&
                dayFlowController.CurrentEmergencyObjectiveType == EmergencyObjectiveType.StoryRecordInspection)
            {
                FieldModeController fieldModeController = FindFirstObjectByType<FieldModeController>(FindObjectsInactive.Include);
                fieldModeController?.PlayPowerRestoreLightEffect();
                SoundManager.Instance?.PlayBreakerPowerOnSfx();
            }

            dayFlowController?.CompleteEmergencyObjective();
        }
    }

    private void RefreshRange()
    {
        bool inRange = player != null && Vector2.Distance(player.position, transform.position) <= interactionRange;
        if (inRange == IsPlayerInRange)
            return;

        IsPlayerInRange = inRange;
        SetOutlineVisible(inRange && !IsCompleted && !IsInspecting);
    }

    private void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<FieldPlayerMovementController>(FindObjectsInactive.Include);
        if (player == null && playerMovement != null)
            player = playerMovement.transform;
        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);
        if (recordCollection == null)
            recordCollection = GetComponentInParent<FieldStoryRecordCollection>(true);
    }

    private void ResolveOutlineVisual()
    {
        if (outlineVisual != null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && string.Equals(child.name, "Outline", StringComparison.OrdinalIgnoreCase))
            {
                outlineVisual = child.gameObject;
                break;
            }
        }
    }

    private void SetOutlineVisible(bool visible)
    {
        if (outlineVisual != null && outlineVisual.activeSelf != visible)
            outlineVisual.SetActive(visible);
    }

    private void SetPopupVisible(bool visible)
    {
        if (inspectPopupRoot != null && inspectPopupRoot.activeSelf != visible)
            inspectPopupRoot.SetActive(visible);
    }

    private void ShowRecordPopup()
    {
        if (recordCollection != null)
        {
            recordCollection.ShowRecord(recordTitle, recordBody);
            return;
        }

        if (inspectPopup != null)
        {
            inspectPopup.Show(recordTitle, recordBody);
            return;
        }

        SetPopupVisible(true);
    }

    private void HideRecordPopup()
    {
        if (recordCollection != null)
        {
            recordCollection.HideRecord();
            return;
        }

        if (inspectPopup != null)
        {
            inspectPopup.Hide();
            return;
        }

        SetPopupVisible(false);
    }

    private void OnDisable()
    {
        IsInspecting = false;
        SetOutlineVisible(false);
        HideRecordPopup();
        playerMovement?.SetInputEnabled(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
