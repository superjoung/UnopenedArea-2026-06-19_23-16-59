using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 현재 진행 상태와 다음 행동을 안내하는 메인룸 UI입니다.
/// 상태 변경 직후가 아니라, 필요한 시간 및 화면 전환이 끝난 뒤에만 문구를 표시합니다.
/// </summary>
public class MainSceneUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;
    [SerializeField] private TransitionEffect transitionEffect;

    [Header("Text")]
    [SerializeField] private TMP_Text situationText;
    [SerializeField] private TMP_Text objectiveText;

    [Header("Visibility")]
    [SerializeField] private bool hideWhileViewingCctv = true;
    [Tooltip("상황/행동 텍스트만 담는 루트입니다. CCTV 전환 패널은 이 루트 밖에 두십시오.")]
    [SerializeField] private GameObject textContentRoot;

    [Header("Message Timing")]
    [SerializeField, Min(0f)] private float phoneRingingMessageDelay = 0.5f;
    [SerializeField, Min(0f)] private float monitoringAssignedMessageDelay = 0.5f;

    private Day1FlowController subscribedFlowController;
    private Day1AreaTransitionController subscribedAreaTransitionController;
    private TransitionEffect subscribedTransitionEffect;
    private Coroutine messageDisplayRoutine;
    private bool wasTransitionPlaying;
    private bool externalMessageActive;
    private bool temporarilySuppressed;

    public GameObject TextContentRoot => textContentRoot;
    public TMP_Text SituationText => situationText;
    public TMP_Text ObjectiveText => objectiveText;

    /// <summary>Shows TempUI for a one-off presentation that is not tied to flow state.</summary>
    public void ShowExternalMessage(string situation, string objective)
    {
        externalMessageActive = true;
        CancelScheduledMessage();
        ApplyMessage(situation, objective);
        SetVisible(true);
    }

    public void HideExternalMessage()
    {
        externalMessageActive = false;
        SetVisible(false);
    }

    /// <summary>스토리 대화처럼 다른 화면 UI를 단독으로 보여줄 때 안내 텍스트를 잠시 숨깁니다.</summary>
    public void SetTemporarilySuppressed(bool suppressed)
    {
        temporarilySuppressed = suppressed;
        if (suppressed)
        {
            CancelScheduledMessage();
            SetVisible(false);
            return;
        }

        Refresh();
    }

    private void Awake()
    {
        ResolveReferences();

        // 이전 버전의 CanvasGroup이 남아 있다면 Canvas 전체를 숨기지 않도록 복구한다.
        CanvasGroup legacyCanvasGroup = GetComponent<CanvasGroup>();
        if (legacyCanvasGroup != null)
        {
            legacyCanvasGroup.alpha = 1f;
            legacyCanvasGroup.interactable = true;
            legacyCanvasGroup.blocksRaycasts = true;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        bool isTransitionPlaying = transitionEffect != null && transitionEffect.IsPlaying;
        if (isTransitionPlaying)
        {
            if (!wasTransitionPlaying)
            {
                CancelScheduledMessage();
                SetVisible(false);
            }

            wasTransitionPlaying = true;
            return;
        }

        if (wasTransitionPlaying)
        {
            wasTransitionPlaying = false;
            Refresh();
        }
    }

    private void OnDisable()
    {
        CancelScheduledMessage();
        Unsubscribe();
    }

    public void Refresh()
    {
        if (temporarilySuppressed)
        {
            CancelScheduledMessage();
            SetVisible(false);
            return;
        }

        if (externalMessageActive)
        {
            SetVisible(true);
            return;
        }

        Day1FlowState flowState = day1FlowController != null
            ? day1FlowController.State
            : Day1FlowState.None;
        Day1AreaMode areaMode = areaTransitionController != null
            ? areaTransitionController.CurrentMode
            : Day1AreaMode.None;

        GetMessage(flowState, areaMode, out string situation, out string objective);
        ApplyMessage(situation, objective);

        bool shouldHide = (day1FlowController != null && day1FlowController.IsAwaitingTitleStart) ||
                          (hideWhileViewingCctv && areaMode == Day1AreaMode.CCTV) ||
                          (transitionEffect != null && transitionEffect.IsPlaying) ||
                          flowState == Day1FlowState.None ||
                          flowState == Day1FlowState.Completed ||
                          flowState == Day1FlowState.Failed;
        if (shouldHide)
        {
            CancelScheduledMessage();
            SetVisible(false);
            return;
        }

        ScheduleMessageDisplay(GetMessageDelay(flowState));
    }

    private void HandleFlowStateChanged(Day1FlowState state)
    {
        Refresh();
    }

    private void HandleAreaModeChanged(Day1AreaMode mode)
    {
        Refresh();
    }

    private void HandleTransitionPlaybackChanged(bool isPlaying)
    {
        if (!isPlaying)
            return;

        // 정전의 첫 깜빡임과 같은 프레임에 즉시 숨긴다.
        wasTransitionPlaying = true;
        CancelScheduledMessage();
        SetVisible(false);
    }

    private static void GetMessage(Day1FlowState flowState, Day1AreaMode areaMode, out string situation, out string objective)
    {
        situation = string.Empty;
        objective = string.Empty;

        switch (flowState)
        {
            case Day1FlowState.Briefing:
                situation = "전화가 울리고 있다.";
                objective = "전화를 받으시오.";
                return;

            case Day1FlowState.BaselineReview:
                situation = "감시 업무가 배정되었습니다.";
                objective = "CCTV를 확인하십시오.";
                return;

            case Day1FlowState.Monitoring:
                if (areaMode == Day1AreaMode.MainRoom)
                {
                    situation = "감시 업무가 배정되었습니다.";
                    objective = "CCTV를 확인하십시오.";
                    return;
                }

                situation = "감시를 진행 중입니다.";
                objective = "이상 현상을 발견하면 보고하십시오.";
                return;

            case Day1FlowState.EmergencyDispatch:
                situation = "정전이 발생했다.";
                objective = areaMode == Day1AreaMode.Field
                    ? "배전반을 찾아 E를 꾹 눌러 고치세요."
                    : "메인룸의 문으로 제어실 외부로 나가십시오.";
                return;

            case Day1FlowState.EmergencyRecovery:
                if (areaMode == Day1AreaMode.Field)
                {
                    situation = "전력이 복구되었다.";
                    objective = "문으로 메인룸에 돌아가십시오.";
                }
                else
                {
                    situation = "메인룸에 복귀했다.";
                    objective = "CCTV를 다시 확인하십시오.";
                }
                return;

            case Day1FlowState.Completed:
            case Day1FlowState.Failed:
                return;

            default:
                return;
        }
    }

    private void Subscribe()
    {
        if (day1FlowController != null && subscribedFlowController != day1FlowController)
        {
            UnsubscribeFlowController();
            subscribedFlowController = day1FlowController;
            subscribedFlowController.StateChanged += HandleFlowStateChanged;
        }

        if (areaTransitionController != null && subscribedAreaTransitionController != areaTransitionController)
        {
            UnsubscribeAreaTransitionController();
            subscribedAreaTransitionController = areaTransitionController;
            subscribedAreaTransitionController.ModeChanged += HandleAreaModeChanged;
        }

        if (transitionEffect != null && subscribedTransitionEffect != transitionEffect)
        {
            UnsubscribeTransitionEffect();
            subscribedTransitionEffect = transitionEffect;
            subscribedTransitionEffect.PlaybackChanged += HandleTransitionPlaybackChanged;
        }
    }

    private void Unsubscribe()
    {
        UnsubscribeFlowController();
        UnsubscribeAreaTransitionController();
        UnsubscribeTransitionEffect();
    }

    private void UnsubscribeFlowController()
    {
        if (subscribedFlowController == null)
            return;

        subscribedFlowController.StateChanged -= HandleFlowStateChanged;
        subscribedFlowController = null;
    }

    private void UnsubscribeAreaTransitionController()
    {
        if (subscribedAreaTransitionController == null)
            return;

        subscribedAreaTransitionController.ModeChanged -= HandleAreaModeChanged;
        subscribedAreaTransitionController = null;
    }

    private void UnsubscribeTransitionEffect()
    {
        if (subscribedTransitionEffect == null)
            return;

        subscribedTransitionEffect.PlaybackChanged -= HandleTransitionPlaybackChanged;
        subscribedTransitionEffect = null;
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();

        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();

        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
    }

    private float GetMessageDelay(Day1FlowState flowState)
    {
        switch (flowState)
        {
            case Day1FlowState.Briefing:
                return phoneRingingMessageDelay;
            case Day1FlowState.BaselineReview:
                return monitoringAssignedMessageDelay;
            default:
                return 0f;
        }
    }

    private void ApplyMessage(string situation, string objective)
    {
        if (situationText != null)
            situationText.text = situation;

        if (objectiveText != null)
            objectiveText.text = objective;
    }

    private void ScheduleMessageDisplay(float delay)
    {
        CancelScheduledMessage();
        SetVisible(false);
        messageDisplayRoutine = StartCoroutine(ShowMessageAfterDelay(delay));
    }

    private IEnumerator ShowMessageAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Day1AreaMode areaMode = areaTransitionController != null
            ? areaTransitionController.CurrentMode
            : Day1AreaMode.None;
        bool canShow = (transitionEffect == null || !transitionEffect.IsPlaying) &&
                       (!hideWhileViewingCctv || areaMode != Day1AreaMode.CCTV) &&
                       (day1FlowController == null || !day1FlowController.IsAwaitingTitleStart);
        if (canShow)
            SetVisible(true);

        messageDisplayRoutine = null;
    }

    private void CancelScheduledMessage()
    {
        if (messageDisplayRoutine == null)
            return;

        StopCoroutine(messageDisplayRoutine);
        messageDisplayRoutine = null;
    }

    private void SetVisible(bool visible)
    {
        if (textContentRoot != null)
        {
            textContentRoot.SetActive(visible);
            return;
        }

        if (situationText != null)
            situationText.gameObject.SetActive(visible);

        if (objectiveText != null)
            objectiveText.gameObject.SetActive(visible);
    }
}
