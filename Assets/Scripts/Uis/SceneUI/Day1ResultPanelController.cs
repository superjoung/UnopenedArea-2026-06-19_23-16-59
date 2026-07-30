using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Day 1 종료 결과를 메인룸에서 표시합니다.
/// 실제 미보고 3회 실패만 CCTVRoom을 잠시 보여준 후 결과를 표시합니다.
/// </summary>
public class Day1ResultPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;
    [SerializeField] private TransitionEffect transitionEffect;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject againButton;
    [SerializeField] private GameObject nextButton;

    [Header("Failed By Three Misses")]
    [SerializeField, Min(0f)] private float cctvRoomRevealDuration = 5f;

    private Coroutine presentationRoutine;

    private void Awake()
    {
        ResolveReferences();
        SetResultPanelVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (day1FlowController != null)
            day1FlowController.StateChanged += HandleFlowStateChanged;
    }

    private void OnDisable()
    {
        if (day1FlowController != null)
            day1FlowController.StateChanged -= HandleFlowStateChanged;

        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);
        presentationRoutine = null;
    }

    private void HandleFlowStateChanged(Day1FlowState state)
    {
        if (state != Day1FlowState.Completed && state != Day1FlowState.Failed)
            return;

        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);

        bool isSuccess = state == Day1FlowState.Completed;
        bool isThirdMissFailure = !isSuccess && dayRuntimeController != null &&
                                  dayRuntimeController.MissedAnomalyCount >= 3;
        presentationRoutine = StartCoroutine(PresentResultRoutine(isSuccess, isThirdMissFailure));
    }

    private IEnumerator PresentResultRoutine(bool isSuccess, bool revealCctvRoom)
    {
        if (revealCctvRoom && cctvRoomRevealDuration > 0f)
            yield return new WaitForSecondsRealtime(cctvRoomRevealDuration);

        ResolveReferences();
        if (areaTransitionController != null && areaTransitionController.CurrentMode == Day1AreaMode.CCTV)
        {
            bool transitionStarted = transitionEffect != null &&
                                     transitionEffect.TryPlayCctvExit(areaTransitionController.EnterMainRoom);
            if (transitionStarted)
            {
                while (transitionEffect != null && transitionEffect.IsPlaying)
                    yield return null;
            }
            else if (transitionEffect == null || !transitionEffect.IsPlaying)
            {
                areaTransitionController.EnterMainRoom();
            }
        }
        else
        {
            areaTransitionController?.EnterMainRoom();
        }

        ShowResult(isSuccess);
        presentationRoutine = null;
    }

    private void ShowResult(bool isSuccess)
    {
        if (stateText != null)
            stateText.text = isSuccess ? "성공" : "실패";

        if (againButton != null)
            againButton.SetActive(true);
        if (nextButton != null)
            nextButton.SetActive(isSuccess);

        SetResultPanelVisible(true);
    }

    private void SetResultPanelVisible(bool visible)
    {
        if (resultPanel != null)
            resultPanel.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
    }
}
