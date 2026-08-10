using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Day1FlowState
{
    None = 0,
    Briefing = 1,
    BaselineReview = 2,
    Monitoring = 3,
    EmergencyDispatch = 4,
    EmergencyRecovery = 5,
    Completed = 6,
    Failed = 7,
}

/// <summary>
/// Day 1의 고정 진행을 담당한다. UI가 연결되기 전에는 Enter 키로 브리핑과
/// 기준 상태 확인을 넘길 수 있으며, 이후 UI는 공개 메서드를 호출하면 된다.
/// </summary>
public class Day1FlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private AnomalyScheduler anomalyScheduler;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private FieldModeController fieldModeController;
    [SerializeField] private Day1AreaTransitionController areaTransitionController;
    [SerializeField] private TransitionEffect transitionEffect;
    [SerializeField] private CCTVScreenEffectController screenEffectController;
    [SerializeField] private CCTVSceneUI cctvSceneUI;
    [SerializeField] private StoryDialogueController storyDialogueController;
    [SerializeField] private TutorialPanelController tutorialPanelController;
    [SerializeField] private CCTVKeyTutorialController cctvKeyTutorialController;
    [SerializeField] private MainRoomStateEffectController mainRoomStateEffectController;
    [SerializeField] private Day2BlackoutForeshadowController day2BlackoutForeshadowController;
    [SerializeField] private MissedAnomalyWarningOverlay missedAnomalyWarningOverlay;

    [Header("Tutorial Presentation")]
    [SerializeField, Min(0f)] private float tutorialChannelActivationDelay = 0.5f;

    [Header("Emergency Dispatch Timing")]
    [Tooltip("정답 보고 직후에는 조건을 충족해도 이 시간만큼 정전 시작을 보류합니다.")]
    [SerializeField, Min(0f)] private float minimumEmergencyDelayAfterCorrectReport = 3f;

    [Header("Temporary Debug Input")]
    [SerializeField] private bool useKeyboardAdvance = false;
    [SerializeField] private KeyCode emergencyRecoveryKey = KeyCode.E;
    [SerializeField] private bool enableDebugEmergencyShortcut = true;
    [SerializeField] private KeyCode debugEmergencyKey = KeyCode.F7;
    [SerializeField] private KeyCode cctvExitKey = KeyCode.Z;

    private DayDefinition dayDefinition;
    private int channelSwitchCount;
    private readonly HashSet<AreaId> baselineObservedAreaIds = new HashSet<AreaId>();
    private bool initialBaselineReviewFinished;
    private bool tutorialActivationRequested;
    private bool tutorialFinished;
    private bool cctvKeyTutorialActive;
    private bool cctvKeyTutorialPanCompleted;
    private bool cctvKeyTutorialReportAvailable;
    private bool emergencyDispatchStarted;
    private bool emergencyFieldModeStarted;
    private bool emergencySequenceCompleted;
    private Coroutine tutorialChannelActivationRoutine;
    private Coroutine objectiveMessageRoutine;
    private Coroutine emergencyDispatchRoutine;
    private PresentationLockMode presentationLockMode;
    private bool presentationPausedDay;
    private bool presentationGenerationWasPaused;
    private bool presentationAnomalyTimersWerePaused;
    private bool presentationCctvInputWasEnabled;
    private bool presentationReportInputWasEnabled;
    private int observedSuccessReportCount;
    private float lastCorrectReportTime = float.NegativeInfinity;

    private enum PresentationLockMode
    {
        None = 0,
        Emergency = 1,
    }

    public Day1FlowState State { get; private set; } = Day1FlowState.None;
    public int ChannelSwitchCount => channelSwitchCount;
    public bool TutorialFinished => tutorialFinished;
    public bool IsCctvKeyTutorialActive => cctvKeyTutorialActive;
    public bool IsPresentationLocked => presentationLockMode != PresentationLockMode.None;
    public bool IsEmergencyPresentationLocked => presentationLockMode == PresentationLockMode.Emergency;
    public bool IsTerminalFailurePresentationActive { get; private set; }
    public bool IsAwaitingTitleStart { get; private set; }
    public bool IsEmergencyFieldModeStarted => emergencyFieldModeStarted;
    public int DayNumber => dayDefinition != null ? dayDefinition.Day : 0;
    /// <summary>
    /// 시간 만료로 하루를 끝내도 되는지 나타냅니다. 현장이동이 설정된 일차는
    /// 현장 목표를 해결하고 CCTV 감시로 복귀하기 전까지 시간 종료를 보류합니다.
    /// </summary>
    public bool CanCompleteTimedDay => dayDefinition == null ||
                                       !dayDefinition.EnableEmergencyDispatch ||
                                       emergencySequenceCompleted;
    public EmergencyObjectiveType CurrentEmergencyObjectiveType => dayDefinition != null
        ? dayDefinition.EmergencyObjectiveType
        : global::EmergencyObjectiveType.PowerRestore;

    public System.Action<Day1FlowState> StateChanged;
    public System.Action<string> FlowMessageChanged;

    protected virtual void Awake()
    {
        ResolveReferences();
    }

    protected virtual void Start()
    {
        ResolveReferences();

        if (dayRuntimeController == null || dayRuntimeController.CurrentDayDefinition == null)
        {
            Debug.LogWarning("[DayFlowController] DayDefinition is missing. Day flow is disabled.");
            enabled = false;
            return;
        }

        dayDefinition = dayRuntimeController.CurrentDayDefinition;
        observedSuccessReportCount = dayRuntimeController.SuccessReportCount;

        SubscribeEvents();
        areaTransitionController?.EnterMainRoom();

        DayTitleController titleController = FindFirstObjectByType<DayTitleController>();
        // DayTitleController가 있으면 타이틀 표시 여부와 관계없이 해당 컨트롤러가
        // 전화 시작 시점(일반 시작/재시작/다음 날 지연)을 전담한다.
        // WaitForPlayerStart만 확인하면 타이틀을 건너뛴 재시작에서 이 Start가
        // 지연 코루틴보다 먼저 Briefing을 시작해 전화가 즉시 울리게 된다.
        IsAwaitingTitleStart = titleController != null;
        if (titleController == null)
            BeginDayBriefing();
    }

    protected virtual void Update()
    {
        if (PausePanelController.IsPaused)
            return;

        TrackCorrectReportTiming();

        if (Input.GetKeyDown(cctvExitKey))
            ExitCCTVToMainRoom();

        if (useKeyboardAdvance && Input.GetKeyDown(KeyCode.Return))
        {
            if (State == Day1FlowState.Briefing)
                BeginBaselineReview();
            else if (State == Day1FlowState.BaselineReview)
                BeginMonitoring();
        }

        if (State == Day1FlowState.Monitoring && initialBaselineReviewFinished)
        {
            if (RequiresTutorial && !tutorialActivationRequested)
                TryStartTutorialAnomaly();
            else if (!RequiresTutorial || tutorialFinished)
                TryStartEmergencyDispatch();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enableDebugEmergencyShortcut && Input.GetKeyDown(debugEmergencyKey))
            ForceEmergencyDispatchForDebug();
#endif

    }

    protected virtual void OnDestroy()
    {
        if (tutorialChannelActivationRoutine != null)
            StopCoroutine(tutorialChannelActivationRoutine);
        if (objectiveMessageRoutine != null)
            StopCoroutine(objectiveMessageRoutine);
        if (emergencyDispatchRoutine != null)
            StopCoroutine(emergencyDispatchRoutine);

        UnsubscribeEvents();
    }

    /// <summary>
    /// 타이틀 오버레이를 닫은 뒤 호출하는 해당 일차의 첫 브리핑 시작점입니다.
    /// 타이틀 컨트롤러가 없는 기존 테스트 씬에서는 Start에서 자동 호출됩니다.
    /// </summary>
    public void BeginDayBriefing()
    {
        if (State != Day1FlowState.None || dayDefinition == null)
            return;

        IsAwaitingTitleStart = false;
        if (RequiresTutorial)
            PauseNormalAnomalies();

        ChangeState(Day1FlowState.Briefing,
            $"DAY {dayDefinition.Day} 감시 기록을 시작합니다. 메인룸의 전화기를 확인하십시오.");
    }

    public void BeginBaselineReview()
    {
        if (State != Day1FlowState.Briefing)
            return;

        ChangeState(Day1FlowState.BaselineReview, "전화 지시를 수령했습니다. 메인룸의 CCTV를 눌러 감시를 시작하십시오.");
    }

    public void AcceptPhoneMission()
    {
        if (State == Day1FlowState.Briefing)
        {
            SoundManager.Instance?.StopPhoneRingAndPlayHangupSfx();

            if (mainRoomStateEffectController == null)
                mainRoomStateEffectController = FindFirstObjectByType<MainRoomStateEffectController>();
            mainRoomStateEffectController?.StopPhoneRingingImmediately();

            if (storyDialogueController == null)
                storyDialogueController = FindFirstObjectByType<StoryDialogueController>();

            if (storyDialogueController != null && storyDialogueController.HasDialogue)
            {
                storyDialogueController.PlayPhoneDialogue(ContinueAfterPhoneDialogue);
                return;
            }

            ContinueAfterPhoneDialogue();
        }
    }

    private void ContinueAfterPhoneDialogue()
    {
        if (dayDefinition != null && dayDefinition.Day == 1)
        {
            if (tutorialPanelController == null)
                tutorialPanelController = FindFirstObjectByType<TutorialPanelController>(FindObjectsInactive.Include);

            if (tutorialPanelController != null)
            {
                tutorialPanelController.Show(BeginBaselineReview);
                return;
            }
        }

        BeginBaselineReview();
    }

    /// <summary>
    /// 메인룸 CCTV 상호작용에서 호출합니다. 첫 진입은 감시 시작, 복구 뒤 진입은 감시 재개입니다.
    /// </summary>
    public void EnterCCTVFromMainRoom()
    {
        if (State != Day1FlowState.BaselineReview &&
            State != Day1FlowState.EmergencyRecovery &&
            State != Day1FlowState.Monitoring)
            return;

        if (areaTransitionController != null && !areaTransitionController.EnterCCTV())
            return;

        if (State == Day1FlowState.BaselineReview)
        {
            BeginMonitoring();
            cctvSceneUI?.PlayInitialMonitoringIntro();
            return;
        }

        if (State == Day1FlowState.Monitoring)
            return;

        emergencySequenceCompleted = true;
        ResumeAfterEmergencyDispatch();
        ChangeState(Day1FlowState.Monitoring, "CCTV 감시를 재개합니다.");
    }

    /// <summary>
    /// CCTV UI의 '제어실 확인' 버튼이 호출합니다.
    /// 감시 중에는 메인룸을 잠깐 확인할 수 있고, 근무/이상현상 타이머는 계속 진행됩니다.
    /// </summary>
    public void ExitCCTVToMainRoom()
    {
        if (State != Day1FlowState.Monitoring ||
            areaTransitionController == null ||
            areaTransitionController.CurrentMode != Day1AreaMode.CCTV ||
            cctvKeyTutorialActive)
            return;

        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();

        if (transitionEffect != null)
        {
            if (transitionEffect.TryPlayCctvExit(areaTransitionController.EnterMainRoom))
                return;

            if (transitionEffect.IsPlaying)
                return;
        }

        areaTransitionController.EnterMainRoom();
    }

    /// <summary>메인룸 문 상호작용에서 호출합니다.</summary>
    public void EnterFieldFromMainRoom()
    {
        if (State != Day1FlowState.EmergencyDispatch || emergencyFieldModeStarted)
            return;

        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();

        if (areaTransitionController != null && transitionEffect != null)
        {
            if (transitionEffect.TryPlayDoorToField(
                    () => TryEnterFieldFromMainRoom(),
                    NotifyFieldEntered))
            {
                return;
            }

            if (transitionEffect.IsPlaying)
                return;
        }

        if (!TryEnterFieldFromMainRoom())
            return;

        NotifyFieldEntered();
    }

    private bool TryEnterFieldFromMainRoom()
    {
        if (State != Day1FlowState.EmergencyDispatch || emergencyFieldModeStarted)
            return false;

        bool entered = areaTransitionController != null
            ? areaTransitionController.EnterField()
            : fieldModeController != null && fieldModeController.EnterFieldMode();
        if (!entered)
            return false;

        emergencyFieldModeStarted = true;
        return true;
    }

    private void NotifyFieldEntered()
    {
        if (!emergencyFieldModeStarted)
            return;

        FlowMessageChanged?.Invoke(GetEmergencyFieldObjectiveMessage());
    }

    public void BeginMonitoring()
    {
        if (State != Day1FlowState.BaselineReview)
            return;

        channelSwitchCount = 0;
        baselineObservedAreaIds.Clear();
        tutorialActivationRequested = false;
        tutorialFinished = !RequiresTutorial;
        cctvKeyTutorialActive = false;
        cctvKeyTutorialPanCompleted = false;
        cctvKeyTutorialReportAvailable = false;
        emergencyDispatchStarted = false;
        emergencyFieldModeStarted = false;
        emergencySequenceCompleted = false;

        if (RequiresInitialBaselineReview && sceneController?.CurrentChannel?.Area != null)
            baselineObservedAreaIds.Add(sceneController.CurrentChannel.Area.AreaId);

        initialBaselineReviewFinished = !RequiresInitialBaselineReview ||
                                        HasObservedEveryBaselineArea();

        if (dayRuntimeController != null)
        {
            dayRuntimeController.StartDay();
            if (!initialBaselineReviewFinished)
                dayRuntimeController.PauseDay();
        }

        // DAY1 기준 화면 확인 또는 첫 튜토리얼 이상현상이 끝날 때까지 랜덤 이벤트는 시작하지 않는다.
        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(RequiresTutorial || !initialBaselineReviewFinished);

        ChangeState(Day1FlowState.Monitoring, "감시를 시작합니다. CCTV 채널을 전환해 정상 배치를 확인하십시오.");

        if (UsesCctvKeyTutorial)
            BeginCctvKeyTutorial();
    }

    private void TryStartTutorialAnomaly()
    {
        if (!RequiresTutorial || anomalyService == null)
            return;

        // KeyPanel 조작 안내 중에는 시간 조건으로 단계를 건너뛰지 않는다.
        if (cctvKeyTutorialActive)
            return;

        bool reachedTimeCondition = dayRuntimeController != null &&
                                     dayRuntimeController.ElapsedSec >= dayDefinition.TutorialFallbackElapsedSec;

        if (!reachedTimeCondition)
            return;

        tutorialActivationRequested = true;
        ActivateTutorialAnomaly();
    }

    private void HandleChannelSelected(CCTVChannelRuntime channel)
    {
        if (State != Day1FlowState.Monitoring)
            return;

        if (!initialBaselineReviewFinished && RequiresInitialBaselineReview)
        {
            bool hadObservedEveryArea = HasObservedEveryBaselineArea();
            channelSwitchCount++;
            if (channel?.Area != null)
                baselineObservedAreaIds.Add(channel.Area.AreaId);

            Debug.Log($"[Day1FlowController] Baseline review progress {baselineObservedAreaIds.Count}/{GetBaselineAreaCount()}. channel={channel?.ChannelLabel}");
            // 세 번째 고유 방에 들어온 순간에는 아직 시작하지 않는다.
            // 세 방을 모두 확인한 뒤 다음 채널로 한 번 더 이동했을 때 감시 시간을 시작한다.
            if (hadObservedEveryArea)
                CompleteInitialBaselineReview();
            return;
        }

        if (!RequiresTutorial || tutorialActivationRequested)
            return;

        if (cctvKeyTutorialActive && !cctvKeyTutorialPanCompleted)
            return;

        channelSwitchCount++;
        Debug.Log($"[Day1FlowController] Tutorial channel switch progress {channelSwitchCount}/{dayDefinition.TutorialRequiredChannelSwitches}. channel={channel?.ChannelLabel}");

        if (channelSwitchCount < dayDefinition.TutorialRequiredChannelSwitches)
            return;

        if (cctvKeyTutorialActive)
        {
            // 세 번째 채널 전환 직후 Q/E 안내를 먼저 치운 뒤
            // 대기 시간과 눈 감는 이상현상 등장 연출을 시작한다.
            sceneController?.SetChannelSwitchInputEnabled(false);
            cctvKeyTutorialController?.HideGuide();
        }

        tutorialActivationRequested = true;
        tutorialChannelActivationRoutine = StartCoroutine(ActivateTutorialAfterChannelDelay());
    }

    private IEnumerator ActivateTutorialAfterChannelDelay()
    {
        if (tutorialChannelActivationDelay > 0f)
            yield return new WaitForSecondsRealtime(tutorialChannelActivationDelay);

        if (State == Day1FlowState.Monitoring)
            ActivateTutorialAnomaly();
        else
            tutorialActivationRequested = false;

        tutorialChannelActivationRoutine = null;
    }

    private void CompleteInitialBaselineReview()
    {
        if (initialBaselineReviewFinished)
            return;

        initialBaselineReviewFinished = true;
        dayRuntimeController?.ResumeDay();
        anomalyScheduler?.SetGenerationPaused(RequiresTutorial);

        if (cctvKeyTutorialActive && !RequiresTutorial)
        {
            // 세 번째 채널 전환 직후에는 다른 입력을 잠시 막고,
            // 현재 Day 1 랜덤 풀에서 첫 이상현상을 즉시 시작한다.
            sceneController?.SetChannelSwitchInputEnabled(false);
            anomalyScheduler?.TryTriggerRandomAnomalyNow();
        }

        FlowMessageChanged?.Invoke("모든 CCTV의 정상 상태를 확인했습니다. 이상현상 감시를 시작합니다.");
        Debug.Log($"[Day1FlowController] Baseline review finished. observedAreas={baselineObservedAreaIds.Count}, elapsed={dayRuntimeController?.ElapsedSec ?? 0f:F1}");
    }

    private bool HasObservedEveryBaselineArea()
    {
        int requiredAreaCount = GetBaselineAreaCount();
        return requiredAreaCount > 0 && baselineObservedAreaIds.Count >= requiredAreaCount;
    }

    private int GetBaselineAreaCount()
    {
        return sceneController?.Channels?.Count ?? 0;
    }

    private void ActivateTutorialAnomaly()
    {
        AnomalyRuntime runtime = anomalyService.Activate(dayDefinition.TutorialAnomaly);
        if (runtime == null)
        {
            tutorialActivationRequested = false;
            return;
        }

        FlowMessageChanged?.Invoke(string.Empty);
        Debug.Log($"[Day1FlowController] Tutorial anomaly started. id={dayDefinition.TutorialAnomaly.AnomalyId}, channelSwitches={channelSwitchCount}, elapsed={dayRuntimeController.ElapsedSec:F1}");
    }

    private void HandleAnomalyActivated(AnomalyRuntime runtime)
    {
        if (!cctvKeyTutorialActive || runtime == null)
            return;

        if (RequiresTutorial && !IsTutorialRuntime(runtime))
            return;

        cctvKeyTutorialReportAvailable = true;
        sceneController?.SetChannelSwitchInputEnabled(true);
        cctvKeyTutorialController?.ShowReportGuide();
        GameManager.Instance?.SetReportInputEnabled(true);
    }

    private void BeginCctvKeyTutorial()
    {
        cctvKeyTutorialActive = true;
        cctvKeyTutorialPanCompleted = false;
        cctvKeyTutorialReportAvailable = false;

        sceneController?.SetCCTVInputEnabled(true);
        sceneController?.SetChannelSwitchInputEnabled(false);
        GameManager.Instance?.SetReportInputEnabled(false);
        cctvKeyTutorialController.BeginPanTraining(HandleCctvPanTrainingCompleted);
    }

    private void HandleCctvPanTrainingCompleted()
    {
        if (!cctvKeyTutorialActive || State != Day1FlowState.Monitoring)
            return;

        cctvKeyTutorialPanCompleted = true;
        sceneController?.SetChannelSwitchInputEnabled(true);
        cctvKeyTutorialController?.ShowChannelGuide();
    }

    private void HandleReportPanelVisibilityChanged(bool isOpen)
    {
        if (!isOpen || !cctvKeyTutorialActive || !cctvKeyTutorialReportAvailable)
            return;

        cctvKeyTutorialActive = false;
        cctvKeyTutorialController?.HideGuide();
    }

    /// <summary>
    /// 첫 진입 준비 연출이 끝났을 때 현재 KeyPanel 단계가 허용하는 입력만 복구합니다.
    /// </summary>
    public bool RestoreCctvKeyTutorialInputStateAfterIntro()
    {
        if (!cctvKeyTutorialActive)
            return false;

        sceneController?.SetCCTVInputEnabled(true);
        sceneController?.SetChannelSwitchInputEnabled(cctvKeyTutorialPanCompleted);
        GameManager.Instance?.SetReportInputEnabled(cctvKeyTutorialReportAvailable);
        return true;
    }

    private void HandleAnomalyResolved(AnomalyRuntime runtime)
    {
        if (!IsTutorialRuntime(runtime))
            return;

        FinishTutorial(true);
    }

    private void HandleAnomalyMissed(AnomalyRuntime runtime)
    {
        if (!IsTutorialRuntime(runtime))
            return;

        FinishTutorial(false);
    }

    private bool IsTutorialRuntime(AnomalyRuntime runtime)
    {
        return !tutorialFinished &&
               tutorialActivationRequested &&
               runtime != null &&
               runtime.Definition == dayDefinition.TutorialAnomaly;
    }

    private void FinishTutorial(bool resolved)
    {
        tutorialFinished = true;
        cctvKeyTutorialActive = false;
        cctvKeyTutorialController?.HideGuide();
        sceneController?.SetChannelSwitchInputEnabled(true);
        GameManager.Instance?.SetReportInputEnabled(true);

        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(false);

        if (resolved)
        {
            ShowTimedObjectiveMessage("첫 기록이 정상적으로 처리되었습니다.");
        }
        else
        {
            FlowMessageChanged?.Invoke("첫 기록이 누락되었습니다. 감시를 계속하십시오.");
        }

        Debug.Log($"[Day1FlowController] Tutorial finished. resolved={resolved}");
    }

    public void ShowAnomalyAppearedMessage()
    {
        ShowAnomalyAppearedMessage(null);
    }

    public void ShowAnomalyAppearedMessage(AnomalyRuntime runtime)
    {
        string objective = IsTutorialRuntime(runtime)
            ? "기준 화면과 일치하지 않는 항목을 보고하십시오."
            : "오전 6시까지 이상현상을 보고하시오.";
        ShowTimedObjectiveMessage("이상현상이 발생했습니다.", objective);
    }

    private void ShowTimedObjectiveMessage(string message, string objective = "오전 6시까지 이상현상을 보고하시오.")
    {
        if (objectiveMessageRoutine != null)
            StopCoroutine(objectiveMessageRoutine);

        objectiveMessageRoutine = StartCoroutine(ShowObjectiveMessageRoutine(message, objective));
    }

    private IEnumerator ShowObjectiveMessageRoutine(string message, string objective)
    {
        FlowMessageChanged?.Invoke(message);
        yield return new WaitForSecondsRealtime(2f);
        FlowMessageChanged?.Invoke(objective);
        objectiveMessageRoutine = null;
    }

    private void TryStartEmergencyDispatch()
    {
        if (emergencyDispatchStarted || dayDefinition == null || !dayDefinition.EnableEmergencyDispatch)
            return;

        if (dayRuntimeController == null || dayRuntimeController.State != DayRuntimeState.Running)
            return;

        bool hasRequiredReports = dayRuntimeController.SuccessReportCount >= dayDefinition.EmergencyRequiredCorrectReports;
        float progress = dayRuntimeController.DurationSec <= 0f
            ? 0f
            : dayRuntimeController.ElapsedSec / dayRuntimeController.DurationSec;
        bool reachedProgress = progress >= dayDefinition.EmergencyTriggerProgress;

        bool shouldWaitForConditions = dayDefinition.EmergencyRequiresBothConditions
            ? !hasRequiredReports || !reachedProgress
            : !hasRequiredReports && !reachedProgress;

        // durationSec를 테스트용으로 크게 줄여도 필수 현장이동이 하루 성공보다
        // 먼저 실행되도록 시간 만료를 최종 보장 조건으로 사용한다.
        bool dayTimeExpired = dayRuntimeController.RemainingSec <= 0f;
        if (shouldWaitForConditions && !dayTimeExpired)
            return;

        // 정답을 맞춘 직후 곧바로 정전되는 연출은 시간 만료 시에도 피한다.
        if (Time.unscaledTime < lastCorrectReportTime + minimumEmergencyDelayAfterCorrectReport)
            return;

        BeginEmergencyDispatch(false);
    }

    private void TrackCorrectReportTiming()
    {
        if (dayRuntimeController == null)
            return;

        int successReportCount = dayRuntimeController.SuccessReportCount;
        if (successReportCount > observedSuccessReportCount)
            lastCorrectReportTime = Time.unscaledTime;

        observedSuccessReportCount = successReportCount;
    }

    /// <summary>
    /// 에디터/개발 빌드에서 현장 카메라 전환만 빠르게 검증한다.
    /// 튜토리얼 이상현상이 끝난 뒤 F7을 누르면 일반 이상현상 대기 없이 정전을 시작한다.
    /// </summary>
    public void ForceEmergencyDispatchForDebug()
    {
        if (State != Day1FlowState.Monitoring || (RequiresTutorial && !tutorialFinished) || emergencyDispatchStarted)
        {
            Debug.LogWarning("[Day1FlowController] Debug emergency requires Monitoring state after the tutorial is finished.");
            return;
        }

        BeginEmergencyDispatch(true);
    }

    private void BeginEmergencyDispatch(bool isDebug)
    {
        if (emergencyDispatchStarted)
            return;

        emergencyDispatchStarted = true;
        // 정전 전조(특히 Day 2 Staff 이동) 위에 미보고 5초 붉은 비네트가
        // 겹치지 않도록 현장 전환을 확정하는 즉시 제거한다.
        if (missedAnomalyWarningOverlay == null)
            missedAnomalyWarningOverlay = FindFirstObjectByType<MissedAnomalyWarningOverlay>(FindObjectsInactive.Include);
        missedAnomalyWarningOverlay?.HideForEmergencyTransition();
        emergencyDispatchRoutine = StartCoroutine(BeginEmergencyDispatchRoutine(isDebug));
    }

    private IEnumerator BeginEmergencyDispatchRoutine(bool isDebug)
    {
        while (transitionEffect != null && transitionEffect.IsAnomalyBlinkPlaying)
            yield return null;

        PauseForEmergencyDispatch();

        if (day2BlackoutForeshadowController == null)
            day2BlackoutForeshadowController = FindFirstObjectByType<Day2BlackoutForeshadowController>(FindObjectsInactive.Include);

        if (day2BlackoutForeshadowController != null && day2BlackoutForeshadowController.CanPlay())
            yield return day2BlackoutForeshadowController.Play();

        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();

        if (areaTransitionController != null && transitionEffect != null &&
            transitionEffect.TryPlayBlackout(
                areaTransitionController.EnterMainRoom,
                () => FinishEmergencyDispatch(isDebug)))
        {
            emergencyDispatchRoutine = null;
            yield break;
        }

        FinishEmergencyDispatch(isDebug);
        emergencyDispatchRoutine = null;
    }

    private void FinishEmergencyDispatch(bool isDebug)
    {
        if (areaTransitionController != null)
        {
            emergencyFieldModeStarted = false;
            areaTransitionController.EnterMainRoom();
        }
        else
        {
            // 메인룸 전환 컨트롤러가 아직 연결되지 않은 기존 테스트 씬은 이전 현장 직행 흐름을 유지한다.
            emergencyFieldModeStarted = fieldModeController != null && fieldModeController.EnterFieldMode();
        }

        string message = GetEmergencyDispatchMessage();

        if (isDebug)
            message = $"[DEBUG] {message}";

        ChangeState(Day1FlowState.EmergencyDispatch, message);
    }

    /// <summary>
    /// 현장 맵의 배전반 상호작용이 호출할 복구 완료 진입점이다.
    /// 현장 맵이 준비되기 전에는 emergencyRecoveryKey로 같은 흐름을 검증한다.
    /// </summary>
    public void CompleteEmergencyObjective()
    {
        if (State != Day1FlowState.EmergencyDispatch)
            return;

        // 전력은 복구됐지만 플레이어는 아직 현장에 있다. 제어실 문까지 돌아가기 전에는
        // 타이머와 스케줄러를 재개하지 않아 현장에 있는 플레이어가 보이지 않는 이상현상으로
        // 실패하는 일을 막는다.
        ChangeState(Day1FlowState.EmergencyRecovery, GetEmergencyRecoveryMessage());
    }

    /// <summary>
    /// 현장 맵의 제어실 복귀 문/지점이 호출한다. 화면 복귀를 끝낸 뒤에만 감시 루프를 재개한다.
    /// </summary>
    public void ReturnToControlRoom()
    {
        if (State != Day1FlowState.EmergencyRecovery)
            return;

        if (areaTransitionController != null)
        {
            areaTransitionController.EnterMainRoom();
        }
        else if (emergencyFieldModeStarted && (fieldModeController == null || !fieldModeController.ExitFieldMode()))
        {
            Debug.LogWarning("[Day1FlowController] Control-room return failed because field mode could not exit.");
            return;
        }

        emergencyFieldModeStarted = false;
        FlowMessageChanged?.Invoke("메인룸에 복귀했습니다. CCTV를 눌러 감시 업무를 재개하십시오.");
    }

    private string GetEmergencyDispatchMessage()
    {
        if (dayDefinition != null && !string.IsNullOrWhiteSpace(dayDefinition.EmergencyDispatchText))
            return dayDefinition.EmergencyDispatchText;

        return dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.StoryRecordInspection
            ? "통신과 전력이 불안정하다. 메인룸의 문을 통해 제어실 외부를 확인하십시오."
            : dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.ServerReboot
                ? "서버 경보가 발생했다. 메인룸의 문을 통해 서버실 제어반을 확인하십시오."
                : "정전이 발생했다. 메인룸의 문을 통해 제어실 외부로 나가십시오.";
    }

    private string GetEmergencyFieldObjectiveMessage()
    {
        if (dayDefinition != null && !string.IsNullOrWhiteSpace(dayDefinition.EmergencyFieldObjectiveText))
            return dayDefinition.EmergencyFieldObjectiveText;

        return dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.StoryRecordInspection
            ? "바닥에 수상한 기록물이 떨어져 있다. 가까이 다가가 E 키로 조사하십시오."
            : dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.ServerReboot
                ? "서버 제어반을 조작해 시스템을 재부팅하십시오."
                : "배전반을 찾아 E를 꾹 눌러 전력을 복구하십시오.";
    }

    private string GetEmergencyRecoveryMessage()
    {
        if (dayDefinition != null && !string.IsNullOrWhiteSpace(dayDefinition.EmergencyRecoveryText))
            return dayDefinition.EmergencyRecoveryText;

        return dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.StoryRecordInspection
            ? "시스템이 자동으로 복구되고 있다. 제어실로 돌아가 감시를 재개하십시오."
            : dayDefinition != null && dayDefinition.EmergencyObjectiveType == EmergencyObjectiveType.ServerReboot
                ? "서버 재부팅 완료. 제어실로 돌아가 CCTV 감시를 재개하십시오."
                : "전력 복구 완료. 제어실로 돌아가 CCTV를 재가동하십시오.";
    }

    private void HandleDayCleared()
    {
        ChangeState(Day1FlowState.Completed, "근무 시간이 종료되었습니다.");
    }

    private void HandleDayFailed()
    {
        ChangeState(Day1FlowState.Failed, "인지 편차가 허용치를 초과했습니다.");
    }

    private void HandleTerminalFailureStarted(DayFailureReason reason)
    {
        IsTerminalFailurePresentationActive = true;
        PauseNormalAnomalies();

        if (objectiveMessageRoutine != null)
        {
            StopCoroutine(objectiveMessageRoutine);
            objectiveMessageRoutine = null;
        }

        FlowMessageChanged?.Invoke(string.Empty);
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
        transitionEffect?.CancelAnomalyAppearanceBlink();
        GameManager.Instance?.SetReportInputEnabled(false);
    }

    private void PauseNormalAnomalies()
    {
        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(true);

        if (anomalyService != null)
            anomalyService.SetTimersPaused(true);
    }

    private void PauseForEmergencyDispatch()
    {
        BeginEmergencyPresentationLock();
    }

    private void ResumeAfterEmergencyDispatch()
    {
        EndEmergencyPresentationLock();
    }

    private void BeginEmergencyPresentationLock()
    {
        if (presentationLockMode == PresentationLockMode.Emergency)
            return;

        BeginPresentationLock(PresentationLockMode.Emergency);
    }

    private void EndEmergencyPresentationLock()
    {
        EndPresentationLock(PresentationLockMode.Emergency);
    }

    private void BeginPresentationLock(PresentationLockMode lockMode)
    {
        if (IsPresentationLocked)
            return;

        ResolveReferences();
        presentationLockMode = lockMode;
        presentationPausedDay = dayRuntimeController != null && dayRuntimeController.State == DayRuntimeState.Running;
        presentationGenerationWasPaused = anomalyScheduler != null && anomalyScheduler.GenerationPaused;
        presentationAnomalyTimersWerePaused = anomalyService != null && anomalyService.TimersPaused;
        presentationCctvInputWasEnabled = sceneController == null || sceneController.CCTVInputEnabled;
        presentationReportInputWasEnabled = GameManager.Instance == null || GameManager.Instance.ReportInputEnabled;

        if (presentationPausedDay)
            dayRuntimeController.PauseDay();
        else if (anomalyService != null)
            anomalyService.SetTimersPaused(true);

        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(true);
        if (screenEffectController != null)
            screenEffectController.StopActiveNoise();
        if (sceneController != null)
            sceneController.SetCCTVInputEnabled(false);
        if (GameManager.Instance != null)
            GameManager.Instance.SetReportInputEnabled(false);
    }

    private void EndPresentationLock(PresentationLockMode expectedLockMode)
    {
        if (presentationLockMode != expectedLockMode)
            return;

        if (presentationPausedDay && dayRuntimeController != null)
            dayRuntimeController.ResumeDay();
        else if (anomalyService != null)
            anomalyService.SetTimersPaused(presentationAnomalyTimersWerePaused);

        if (anomalyScheduler != null)
            anomalyScheduler.SetGenerationPaused(presentationGenerationWasPaused);
        if (sceneController != null)
            sceneController.SetCCTVInputEnabled(presentationCctvInputWasEnabled);
        if (GameManager.Instance != null)
            GameManager.Instance.SetReportInputEnabled(presentationReportInputWasEnabled);

        presentationLockMode = PresentationLockMode.None;
    }

    private void ChangeState(Day1FlowState nextState, string message)
    {
        State = nextState;
        StateChanged?.Invoke(nextState);
        FlowMessageChanged?.Invoke(message);
        Debug.Log($"[Day1FlowController] State={nextState}. {message}");
    }

    private void SubscribeEvents()
    {
        if (sceneController != null)
            sceneController.ChannelSelected += HandleChannelSelected;

        if (cctvSceneUI != null)
            cctvSceneUI.ReportPanelVisibilityChanged += HandleReportPanelVisibilityChanged;

        if (anomalyService != null)
        {
            anomalyService.AnomalyActivated += HandleAnomalyActivated;
            anomalyService.AnomalyResolved += HandleAnomalyResolved;
            anomalyService.AnomalyMissed += HandleAnomalyMissed;
        }

        if (dayRuntimeController != null)
        {
            dayRuntimeController.DayCleared += HandleDayCleared;
            dayRuntimeController.DayFailed += HandleDayFailed;
            dayRuntimeController.TerminalFailureStarted += HandleTerminalFailureStarted;
        }
    }

    private void UnsubscribeEvents()
    {
        if (sceneController != null)
            sceneController.ChannelSelected -= HandleChannelSelected;

        if (cctvSceneUI != null)
            cctvSceneUI.ReportPanelVisibilityChanged -= HandleReportPanelVisibilityChanged;

        if (anomalyService != null)
        {
            anomalyService.AnomalyActivated -= HandleAnomalyActivated;
            anomalyService.AnomalyResolved -= HandleAnomalyResolved;
            anomalyService.AnomalyMissed -= HandleAnomalyMissed;
        }

        if (dayRuntimeController != null)
        {
            dayRuntimeController.DayCleared -= HandleDayCleared;
            dayRuntimeController.DayFailed -= HandleDayFailed;
            dayRuntimeController.TerminalFailureStarted -= HandleTerminalFailureStarted;
        }
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (anomalyScheduler == null)
            anomalyScheduler = FindObjectOfType<AnomalyScheduler>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();

        if (sceneController == null)
            sceneController = FindObjectOfType<CCTVTestSceneController>();

        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>();

        if (areaTransitionController == null)
            areaTransitionController = FindFirstObjectByType<Day1AreaTransitionController>();

        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();

        if (screenEffectController == null)
            screenEffectController = FindFirstObjectByType<CCTVScreenEffectController>();

        if (cctvSceneUI == null)
            cctvSceneUI = FindFirstObjectByType<CCTVSceneUI>(FindObjectsInactive.Include);

        if (cctvKeyTutorialController == null)
            cctvKeyTutorialController = FindFirstObjectByType<CCTVKeyTutorialController>(FindObjectsInactive.Include);

        if (storyDialogueController == null)
            storyDialogueController = FindFirstObjectByType<StoryDialogueController>();

        if (tutorialPanelController == null)
            tutorialPanelController = FindFirstObjectByType<TutorialPanelController>(FindObjectsInactive.Include);

        if (mainRoomStateEffectController == null)
            mainRoomStateEffectController = FindFirstObjectByType<MainRoomStateEffectController>();

        if (day2BlackoutForeshadowController == null)
            day2BlackoutForeshadowController = FindFirstObjectByType<Day2BlackoutForeshadowController>(FindObjectsInactive.Include);

        if (missedAnomalyWarningOverlay == null)
            missedAnomalyWarningOverlay = FindFirstObjectByType<MissedAnomalyWarningOverlay>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 튜토리얼 이상현상을 지정한 일차에만 첫 이상현상 전 스케줄러를 멈춥니다.
    /// Day 2 이후처럼 TutorialAnomaly가 비어 있으면 일반 랜덤 풀을 즉시 시작합니다.
    /// </summary>
    protected virtual bool RequiresTutorial => dayDefinition != null && dayDefinition.TutorialAnomaly != null;

    private bool UsesCctvKeyTutorial =>
        dayDefinition != null && dayDefinition.Day == 1 && cctvKeyTutorialController != null;

    /// <summary>
    /// DAY1은 세 CCTV의 정상 상태를 모두 확인하고 다음 채널로 한 번 더 이동한 뒤에만
    /// 시간과 랜덤 이상현상을 시작합니다.
    /// </summary>
    private bool RequiresInitialBaselineReview =>
        dayDefinition != null && dayDefinition.Day == 1 && !RequiresTutorial;
}
