using TMPro;
using UnityEngine;

/// <summary>
/// Day 1의 현재 상황과 다음 행동을 두 줄로 안내합니다.
/// 메인룸 자식이 아닌, 항상 켜져 있는 최상위 Canvas에 붙여 사용합니다.
/// </summary>
public class MainSceneUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;

    [Header("Text")]
    [SerializeField] private TMP_Text situationText;
    [SerializeField] private TMP_Text objectiveText;

    [Header("Visibility")]
    [SerializeField] private bool hideWhileViewingCctv = true;
    [Tooltip("상황/행동 텍스트만 담은 묶음입니다. CCTV 전환 Panel은 이 묶음 밖에 둡니다.")]
    [SerializeField] private GameObject textContentRoot;

    private Day1FlowController subscribedFlowController;
    private Day1AreaTransitionController subscribedAreaTransitionController;

    private void Awake()
    {
        ResolveReferences();

        // 이전 버전이 자동으로 추가한 CanvasGroup이 남아 있다면 전체 Canvas를 숨기지 않도록 복구합니다.
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

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Refresh()
    {
        Day1FlowState flowState = day1FlowController != null
            ? day1FlowController.State
            : Day1FlowState.None;
        Day1AreaMode areaMode = areaTransitionController != null
            ? areaTransitionController.CurrentMode
            : Day1AreaMode.None;

        SetVisible(!hideWhileViewingCctv || areaMode != Day1AreaMode.CCTV);

        GetMessage(flowState, areaMode, out string situation, out string objective);

        if (situationText != null)
            situationText.text = situation;

        if (objectiveText != null)
            objectiveText.text = objective;
    }

    private void HandleFlowStateChanged(Day1FlowState state)
    {
        Refresh();
    }

    private void HandleAreaModeChanged(Day1AreaMode mode)
    {
        Refresh();
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
                situation = "감시 업무가 배정되었다.";
                objective = "CCTV를 확인하시오.";
                return;

            case Day1FlowState.Monitoring:
                situation = "감시를 진행 중이다.";
                objective = "이상 현상을 발견하면 보고하시오.";
                return;

            case Day1FlowState.EmergencyDispatch:
                situation = "정전이 발생했다.";
                objective = areaMode == Day1AreaMode.Field
                    ? "배전반을 찾아 전력을 복구하시오."
                    : "메인룸의 문으로 외부 현장에 나가시오.";
                return;

            case Day1FlowState.EmergencyRecovery:
                if (areaMode == Day1AreaMode.Field)
                {
                    situation = "전력이 복구되었다.";
                    objective = "문으로 메인룸에 돌아가시오.";
                }
                else
                {
                    situation = "메인룸에 복귀했다.";
                    objective = "CCTV를 다시 확인하시오.";
                }
                return;

            case Day1FlowState.Completed:
                situation = "근무가 종료되었다.";
                objective = "결과를 확인하시오.";
                return;

            case Day1FlowState.Failed:
                situation = "감시 업무를 지속할 수 없다.";
                objective = "기록을 확인하시오.";
                return;

            default:
                situation = "상태를 확인 중이다.";
                objective = "잠시 기다리시오.";
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
    }

    private void Unsubscribe()
    {
        UnsubscribeFlowController();
        UnsubscribeAreaTransitionController();
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

    private void ResolveReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();

        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();
    }

    private void SetVisible(bool visible)
    {
        if (textContentRoot != null)
        {
            textContentRoot.SetActive(visible);
            return;
        }

        // Content Root를 따로 만들지 않은 초기 구성도 텍스트 두 개만 숨깁니다.
        if (situationText != null)
            situationText.gameObject.SetActive(visible);

        if (objectiveText != null)
            objectiveText.gameObject.SetActive(visible);
    }
}
