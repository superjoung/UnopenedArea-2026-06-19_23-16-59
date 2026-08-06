using System.Collections;
using DG.Tweening;
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
    [SerializeField] private MainRoomStateEffectController mainRoomStateEffectController;
    [SerializeField] private TransitionEffect transitionEffect;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject againButton;
    [SerializeField] private GameObject nextButton;

    [Header("Success Day Handoff")]
    [Tooltip("Use MainSceneUI's TempUI root. It is automatically found when left empty.")]
    [SerializeField] private GameObject successCompletionPanel;
    [Tooltip("TempUI's StatusText. It is automatically found when left empty.")]
    [SerializeField] private TMP_Text successCompletionLine1;
    [Tooltip("TempUI's ObjectiveText. It is automatically found when left empty.")]
    [SerializeField] private TMP_Text successCompletionLine2;
    [SerializeField, Min(0f)] private float successCompletionDelayAfterReturn = 1f;
    [SerializeField, Min(0f)] private float successCompletionPanelHoldDuration = 3f;
    [SerializeField, Min(0.05f)] private float successCalendarZoomDuration = 1f;
    [SerializeField, Min(0.1f)] private float successCalendarZoomOrthographicSize = 1.25f;
    [SerializeField, Min(0.05f)] private float successCalendarCrossFadeDuration = 0.65f;
    [SerializeField, Min(0f)] private float successCalendarHoldDuration = 0.8f;

    [Header("CCTV Result Presentation")]
    [Tooltip("CCTV 화면에서 잠시 보여줄 성공 표시입니다. 비워두면 별도 표시 없이 1초 대기합니다.")]
    [SerializeField] private GameObject cctvSuccessIndicator;
    [SerializeField, Min(0f)] private float successCctvDisplayDuration = 1f;
    [Tooltip("오보고/마지막 미보고 모두 실패 뒤 CCTV를 유지하는 시간입니다.")]
    [SerializeField, Min(0f)] private float failureCctvObservationDuration = 5f;
    [Tooltip("성공 시 CCTV에서 메인룸으로 돌아온 뒤 결과 UI를 띄우기 전의 정적 구간입니다.")]
    [SerializeField, Min(0f)] private float resultUiDelayAfterMainRoomReturn = 1f;
    [Tooltip("실패 시 메인룸으로 돌아온 뒤 손이 화면을 덮기 전의 정적 구간입니다.")]
    [SerializeField, Min(0f)] private float failureHandDelayAfterMainRoomReturn = 2f;
    [Tooltip("성공/실패 결과로 CCTV를 자동 탈출할 때 사용하는 전체 전환 시간입니다.")]
    [SerializeField, Min(0.1f)] private float automaticCctvExitEffectDuration = 2f;
    [Tooltip("손 연출로 화면이 완전히 검어진 순간 실행됩니다.")]
    [SerializeField] private UnityEvent onFailureEnteredMainRoom;

    [Header("Failure Noise")]
    [SerializeField] private CCTVSceneUI cctvSceneUI;

    [Header("Result Restart")]
    [SerializeField] private bool restartFailedDayOnAnyClick = true;

    [Header("Debug")]
    [Tooltip("F8을 누르면 즉시 하루 성공 처리를 실행합니다. 테스트가 끝나면 끄십시오.")]
    [SerializeField] private bool enableInstantSuccessShortcut = true;

    private Coroutine presentationRoutine;
    private MainSceneUI successMessageUi;
    private bool resultVisible;
    private bool visibleResultIsSuccess;
    private bool restartRequested;

    private void Awake()
    {
        ResolveReferences();
        SetResultPanelVisible(false);
        SetSuccessCompletionVisible(false);
        SetCctvSuccessIndicatorVisible(false);
    }

    private void Update()
    {
        if (enableInstantSuccessShortcut && Input.GetKeyDown(KeyCode.F8))
            DebugCompleteDay();

        if (!restartFailedDayOnAnyClick || !resultVisible || visibleResultIsSuccess || restartRequested)
            return;

        if (Input.GetMouseButtonDown(0))
            RestartFailedDayWithFade();
    }

    /// <summary>Debug-only entry point. Also callable from a temporary Inspector button/event.</summary>
    public void DebugCompleteDay()
    {
        ResolveReferences();
        if (presentationRoutine != null || resultVisible)
            return;

        if (dayRuntimeController == null)
        {
            Debug.LogWarning("[Day1ResultPanelController] Cannot complete the day: DayRuntimeController is missing.");
            return;
        }

        dayRuntimeController.ClearDay();
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

        float mainRoomDelay = isSuccess
            ? successCompletionDelayAfterReturn
            : failureHandDelayAfterMainRoomReturn;
        if (mainRoomDelay > 0f)
            yield return new WaitForSecondsRealtime(mainRoomDelay);

        if (!isSuccess)
        {
            bool handCoverStarted = transitionEffect != null &&
                                    transitionEffect.TryPlayFailureHandCover(
                                        RevealFailureMainRoomState,
                                        null);
            if (handCoverStarted)
            {
                while (transitionEffect != null && transitionEffect.IsPlaying)
                    yield return null;
            }
            else
            {
                RevealFailureMainRoomState();
            }
        }

        if (isSuccess)
            yield return PresentSuccessDayHandoff();
        else
            ShowResult(false);

        presentationRoutine = null;
    }

    private IEnumerator PresentSuccessDayHandoff()
    {
        ShowSuccessCompletionMessage();

        if (successCompletionPanelHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(successCompletionPanelHoldDuration);

        HideSuccessCompletionMessage();
        yield return PlayCalendarAdvancePresentation();
        LoadNextDayWithFade();
    }

    private IEnumerator PlayCalendarAdvancePresentation()
    {
        int completedDay = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : 1;
        GameObject currentDayVisual = mainRoomStateEffectController?.GetCalendarDayVisual(completedDay);
        GameObject nextDayVisual = mainRoomStateEffectController?.GetCalendarDayVisual(completedDay + 1);
        Camera mainCamera = transitionEffect != null ? transitionEffect.MainRoomCamera : Camera.main;

        if (nextDayVisual != null)
            nextDayVisual.SetActive(true);

        SpriteRenderer currentRenderer = currentDayVisual != null
            ? currentDayVisual.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        SpriteRenderer nextRenderer = nextDayVisual != null
            ? nextDayVisual.GetComponentInChildren<SpriteRenderer>(true)
            : null;

        Color currentColor = currentRenderer != null ? currentRenderer.color : Color.white;
        Color nextColor = nextRenderer != null ? nextRenderer.color : Color.white;
        float nextTargetAlpha = nextColor.a;
        if (nextRenderer != null)
        {
            nextColor.a = 0f;
            nextRenderer.color = nextColor;
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        if (mainCamera != null && nextDayVisual != null && mainCamera.orthographic)
        {
            Vector3 cameraTarget = nextDayVisual.transform.position;
            cameraTarget.z = mainCamera.transform.position.z;
            sequence.Append(mainCamera.transform.DOMove(cameraTarget, successCalendarZoomDuration).SetEase(Ease.InOutQuad));
            sequence.Join(mainCamera.DOOrthoSize(successCalendarZoomOrthographicSize, successCalendarZoomDuration).SetEase(Ease.InOutQuad));
        }

        if (currentRenderer != null)
            sequence.Append(currentRenderer.DOFade(0f, successCalendarCrossFadeDuration));
        if (nextRenderer != null)
            sequence.Join(nextRenderer.DOFade(nextTargetAlpha, successCalendarCrossFadeDuration));

        sequence.Play();
        yield return sequence.WaitForCompletion();

        if (currentDayVisual != null)
            currentDayVisual.SetActive(false);
        if (successCalendarHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(successCalendarHoldDuration);
    }

    private void LoadNextDayWithFade()
    {
        int completedDay = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : 1;
        DayProgressSave.AdvanceToNextDay(completedDay);
        DayProgressSave.RequestSkipTitleOnNextSceneLoad();
        string nextSceneName = $"Day{DayProgressSave.CurrentDay}";
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning($"[Day1ResultPanelController] Next day scene is not in Build Settings: {nextSceneName}");
            return;
        }

        bool fadeStarted = transitionEffect != null &&
                           transitionEffect.TryPlaySceneChangeFade(() => SceneManager.LoadScene(nextSceneName));
        if (!fadeStarted)
            SceneManager.LoadScene(nextSceneName);
    }

    private void ShowResult(bool isSuccess)
    {
        if (stateText != null)
            stateText.text = isSuccess ? "성공" : "실패";

        if (againButton != null)
            againButton.SetActive(false);
        if (nextButton != null)
            nextButton.SetActive(false);

        visibleResultIsSuccess = isSuccess;
        resultVisible = true;
        restartRequested = false;
        SetResultPanelVisible(true);
    }

    private void SetResultPanelVisible(bool visible)
    {
        if (resultPanel != null)
            resultPanel.SetActive(visible);
        if (!visible)
            resultVisible = false;
    }

    private void SetSuccessCompletionVisible(bool visible)
    {
        if (successCompletionPanel != null)
            successCompletionPanel.SetActive(visible);
    }

    private void ShowSuccessCompletionMessage()
    {
        ResolveSuccessMessageUi();
        if (successMessageUi != null)
        {
            successMessageUi.ShowExternalMessage("근무를 끝마쳤다.", "다음 날로 넘어갑니다.");
            return;
        }

        SetSuccessCompletionVisible(true);
        if (successCompletionLine1 != null)
            successCompletionLine1.text = "근무를 끝마쳤다.";
        if (successCompletionLine2 != null)
            successCompletionLine2.text = "다음 날로 넘어갑니다.";
    }

    private void HideSuccessCompletionMessage()
    {
        if (successMessageUi != null)
        {
            successMessageUi.HideExternalMessage();
            return;
        }

        SetSuccessCompletionVisible(false);
    }

    private void SetCctvSuccessIndicatorVisible(bool visible)
    {
        if (cctvSuccessIndicator != null)
            cctvSuccessIndicator.SetActive(visible);
    }

    private void RevealFailureMainRoomState()
    {
        mainRoomStateEffectController?.RevealTerminalFailureEffect();
        onFailureEnteredMainRoom?.Invoke();
    }

    private void RestartFailedDayWithFade()
    {
        restartRequested = true;
        ResolveReferences();

        bool fadeStarted = transitionEffect != null &&
                           transitionEffect.TryPlayResultRestartFade(RetryCurrentDay);
        if (!fadeStarted)
            RetryCurrentDay();
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
        if (mainRoomStateEffectController == null)
            mainRoomStateEffectController = FindFirstObjectByType<MainRoomStateEffectController>(FindObjectsInactive.Include);
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
        if (cctvSceneUI == null)
            cctvSceneUI = FindFirstObjectByType<CCTVSceneUI>();

        ResolveSuccessMessageUi();
        if (successMessageUi != null)
        {
            if (successCompletionPanel == null)
                successCompletionPanel = successMessageUi.TextContentRoot;
            if (successCompletionLine1 == null)
                successCompletionLine1 = successMessageUi.SituationText;
            if (successCompletionLine2 == null)
                successCompletionLine2 = successMessageUi.ObjectiveText;
        }
    }

    private void ResolveSuccessMessageUi()
    {
        if (successMessageUi != null && successMessageUi.gameObject.activeInHierarchy)
            return;

        MainSceneUI[] candidates = FindObjectsByType<MainSceneUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MainSceneUI candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                successMessageUi = candidate;
                return;
            }
        }

        if (successMessageUi == null && candidates.Length > 0)
            successMessageUi = candidates[0];
    }
}
