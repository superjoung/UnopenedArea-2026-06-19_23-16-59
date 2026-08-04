using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Day 1 종료 결과를 메인룸에서 표시합니다.
/// 실패 시 마지막 CCTV 화면을 잠시 보여준 후 메인룸에서 결과를 표시합니다.
/// </summary>
public class Day1ResultPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private MainRoomInteractionController mainRoomInteractionController;
    [SerializeField] private TransitionEffect transitionEffect;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject againButton;
    [SerializeField] private GameObject nextButton;

    [Header("CCTV Result Presentation")]
    [Tooltip("CCTV 화면에서 잠시 보여줄 성공 표시입니다. 비워두면 별도 표시 없이 1초 대기합니다.")]
    [SerializeField] private GameObject cctvSuccessIndicator;
    [SerializeField, Min(0f)] private float successCctvDisplayDuration = 1f;
    [Tooltip("오보고/마지막 미보고 모두 실패 뒤 CCTV를 유지하는 시간입니다.")]
    [SerializeField, Min(0f)] private float failureCctvObservationDuration = 5f;
    [Tooltip("CCTV에서 메인룸으로 돌아온 뒤 결과 UI를 띄우기 전의 정적 구간입니다.")]
    [SerializeField, Min(0f)] private float resultUiDelayAfterMainRoomReturn = 1f;
    [Tooltip("성공/실패 결과로 CCTV를 자동 탈출할 때 사용하는 전체 전환 시간입니다.")]
    [SerializeField, Min(0.1f)] private float automaticCctvExitEffectDuration = 2f;
    [Tooltip("CCTV 탈출 후, 실패 UI를 띄우기 직전에 실행됩니다. 메인룸 전용 실패 사건은 나중에 여기에 연결합니다.")]
    [SerializeField] private UnityEvent onFailureEnteredMainRoom;

    [Header("Failure Noise")]
    [SerializeField] private CCTVSceneUI cctvSceneUI;

    private Coroutine presentationRoutine;

    private void Awake()
    {
        ResolveReferences();
        SetResultPanelVisible(false);
        SetCctvSuccessIndicatorVisible(false);
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

        ResolveReferences();
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);

        mainRoomInteractionController?.SetInputLocked(true);
        presentationRoutine = StartCoroutine(PresentResultRoutine(state == Day1FlowState.Completed));
    }

    private IEnumerator PresentResultRoutine(bool isSuccess)
    {
        if (isSuccess)
        {
            SetCctvSuccessIndicatorVisible(true);
            if (successCctvDisplayDuration > 0f)
                yield return new WaitForSecondsRealtime(successCctvDisplayDuration);
            SetCctvSuccessIndicatorVisible(false);
        }
        else
        {
            bool playTerminalFailureNoise = sceneController == null || !sceneController.IsCCTVRoomTakenOver;
            if (playTerminalFailureNoise && cctvSceneUI != null)
                cctvSceneUI.PlayTerminalFailureNoise();

            if (failureCctvObservationDuration > 0f)
                yield return new WaitForSecondsRealtime(failureCctvObservationDuration);
        }

        ResolveReferences();
        mainRoomInteractionController?.SetInputLocked(true);
        if (areaTransitionController != null && areaTransitionController.CurrentMode == Day1AreaMode.CCTV)
        {
            bool transitionStarted = transitionEffect != null &&
                                     transitionEffect.TryPlayCctvExit(
                                         areaTransitionController.EnterMainRoom,
                                         automaticCctvExitEffectDuration);
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

        if (resultUiDelayAfterMainRoomReturn > 0f)
            yield return new WaitForSecondsRealtime(resultUiDelayAfterMainRoomReturn);

        if (!isSuccess)
            onFailureEnteredMainRoom?.Invoke();

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

    private void SetCctvSuccessIndicatorVisible(bool visible)
    {
        if (cctvSuccessIndicator != null)
            cctvSuccessIndicator.SetActive(visible);
    }

    /// <summary>다시하기 버튼에 연결합니다.</summary>
    public void RetryCurrentDay()
    {
        int day = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : 1;
        DayProgressSave.SetCurrentDay(day);
        DayProgressSave.RequestSkipTitleOnNextSceneLoad();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>성공 결과의 다음으로 버튼에 연결합니다.</summary>
    public void ContinueToNextDay()
    {
        int completedDay = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : 1;
        DayProgressSave.AdvanceToNextDay(completedDay);

        string nextSceneName = $"Day{DayProgressSave.CurrentDay}";
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning($"[Day1ResultPanelController] Next day scene is not in Build Settings: {nextSceneName}");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();
        if (sceneController == null)
            sceneController = FindFirstObjectByType<CCTVTestSceneController>();
        if (mainRoomInteractionController == null)
            mainRoomInteractionController = FindFirstObjectByType<MainRoomInteractionController>();
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
        if (cctvSceneUI == null)
            cctvSceneUI = FindFirstObjectByType<CCTVSceneUI>();
    }
}
