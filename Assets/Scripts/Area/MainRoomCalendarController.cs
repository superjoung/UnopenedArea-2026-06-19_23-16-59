using UnityEngine;

/// <summary>
/// 메인룸 달력을 확대하고, 해금된 날짜를 탐색한 뒤 선택한 Day 씬으로 이동합니다.
/// </summary>
public class MainRoomCalendarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainRoomInteractionController interactionController;
    [SerializeField] private MainRoomStateEffectController stateEffectController;
    [SerializeField] private TransitionEffect transitionEffect;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [Tooltip("비워두면 현재 날짜 달력 스프라이트를 확대 중심으로 사용합니다.")]
    [SerializeField] private Transform calendarFocusTarget;
    [Tooltip("SpriteRenderer 등 월드 오브젝트를 넣습니다. 이름 자동 탐색도 지원합니다.")]
    [SerializeField] private GameObject previousArrow;
    [SerializeField] private GameObject nextArrow;

    [Header("Presentation")]
    [SerializeField, Min(0.05f)] private float zoomDuration = 1f;
    [SerializeField, Min(0.1f)] private float zoomOrthographicSize = 1.25f;
    [SerializeField, Min(1)] private int maxSupportedDay = 3;
    [Tooltip("끄면 달력 모드 동안 양쪽 화살표를 모두 표시하고, 이동 불가능한 방향의 클릭만 무시합니다.")]
    [SerializeField] private bool hideUnavailableArrows;

    private static MainRoomCalendarController activeCalendarController;
    private int loadedDay;
    private int selectedDay;
    private bool resumeRuntimeOnClose;
    private bool isOpen;
    private bool inputReady;
    private float previousTimeScale = 1f;
    private bool timeScaleOverridden;

    public static bool IsAnyCalendarModeActive => activeCalendarController != null && activeCalendarController.isOpen;
    public bool IsOpen => isOpen;
    public int SelectedDay => selectedDay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        activeCalendarController = null;
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureArrow(previousArrow, -1);
        ConfigureArrow(nextArrow, 1);
        SetArrowObjectsVisible(false);
    }

    private void Update()
    {
        if (!isOpen || !inputReady || PausePanelController.IsPaused)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            ExitCalendarMode();
    }

    public void EnterCalendarMode()
    {
        ResolveReferences();
        if (isOpen || transitionEffect == null || transitionEffect.IsPlaying)
            return;

        loadedDay = DaySessionLoader.GetLoadedDayOrFallback(DayProgressSave.CurrentDay);
        selectedDay = loadedDay;
        resumeRuntimeOnClose = dayRuntimeController != null && dayRuntimeController.State == DayRuntimeState.Running;
        if (resumeRuntimeOnClose)
            dayRuntimeController.PauseDay();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        timeScaleOverridden = true;

        isOpen = true;
        inputReady = false;
        activeCalendarController = this;
        interactionController?.SetInputLocked(true);
        stateEffectController?.ShowCalendarDayVisual(selectedDay);

        Transform focusTarget = ResolveFocusTarget();
        bool started = transitionEffect.TryPlayMainRoomFocus(
            focusTarget,
            zoomOrthographicSize,
            zoomDuration,
            FinishOpening);
        if (!started)
            FinishOpening();
    }

    public void Navigate(int direction)
    {
        if (!isOpen || !inputReady || direction == 0 || (transitionEffect != null && transitionEffect.IsPlaying))
            return;

        int highestUnlockedDay = Mathf.Min(maxSupportedDay, DayProgressSave.HighestUnlockedDay);
        int nextDay = Mathf.Clamp(selectedDay + (direction < 0 ? -1 : 1), 1, highestUnlockedDay);
        if (nextDay == selectedDay || !DayProgressSave.IsDayUnlocked(nextDay))
            return;

        selectedDay = nextDay;
        stateEffectController?.ShowCalendarDayVisual(selectedDay);
        RefreshArrowVisibility();
    }

    public void ExitCalendarMode()
    {
        if (!isOpen || !inputReady || (transitionEffect != null && transitionEffect.IsPlaying))
            return;

        inputReady = false;
        SetArrowObjectsVisible(false);

        if (selectedDay != loadedDay)
        {
            string sceneName = $"Day{selectedDay}";
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[MainRoomCalendar] Day scene is not in Build Settings: {sceneName}", this);
                inputReady = true;
                RefreshArrowVisibility();
                return;
            }

            bool fadeStarted = transitionEffect != null &&
                               transitionEffect.TryPlaySceneChangeFade(() => DaySessionLoader.LoadDay(selectedDay, true));
            if (!fadeStarted)
                DaySessionLoader.LoadDay(selectedDay, true);
            return;
        }

        bool exitStarted = transitionEffect != null &&
                           transitionEffect.TryPlayMainRoomFocusExit(zoomDuration, FinishClosingCurrentDay);
        if (!exitStarted)
            FinishClosingCurrentDay();
    }

    private void FinishOpening()
    {
        if (!isOpen)
            return;

        inputReady = true;
        RefreshArrowVisibility();
    }

    private void FinishClosingCurrentDay()
    {
        stateEffectController?.RestoreCurrentDayCalendarVisual();
        isOpen = false;
        inputReady = false;
        if (activeCalendarController == this)
            activeCalendarController = null;
        interactionController?.SetInputLocked(false);

        if (resumeRuntimeOnClose)
            dayRuntimeController?.ResumeDay();
        resumeRuntimeOnClose = false;
        RestoreTimeScale();
    }

    private void RefreshArrowVisibility()
    {
        int highestUnlockedDay = Mathf.Min(maxSupportedDay, DayProgressSave.HighestUnlockedDay);
        SetObjectActive(previousArrow, !hideUnavailableArrows || selectedDay > 1);
        SetObjectActive(nextArrow, !hideUnavailableArrows || selectedDay < highestUnlockedDay);
    }

    private void SetArrowObjectsVisible(bool visible)
    {
        SetObjectActive(previousArrow, visible);
        SetObjectActive(nextArrow, visible);
    }

    private Transform ResolveFocusTarget()
    {
        if (calendarFocusTarget != null)
            return calendarFocusTarget;

        GameObject visual = stateEffectController != null
            ? stateEffectController.GetCalendarDayVisual(selectedDay)
            : null;
        return visual != null ? visual.transform : transform;
    }

    private void ResolveReferences()
    {
        if (interactionController == null)
            interactionController = GetComponent<MainRoomInteractionController>();
        if (stateEffectController == null)
            stateEffectController = GetComponent<MainRoomStateEffectController>();
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (previousArrow == null)
            previousArrow = FindChildByNames("LArrow", "CalendarPreviousArrow", "CalendarPrevArrow", "ArrowLeft", "LeftArrow");
        if (nextArrow == null)
            nextArrow = FindChildByNames("RArrow", "CalendarNextArrow", "ArrowRight", "RightArrow");

        ConfigureArrow(previousArrow, -1);
        ConfigureArrow(nextArrow, 1);
    }

    private GameObject FindChildByNames(params string[] names)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            foreach (string candidate in names)
            {
                if (string.Equals(child.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
            }
        }

        return null;
    }

    private void ConfigureArrow(GameObject arrowObject, int direction)
    {
        if (arrowObject == null)
            return;

        CalendarNavigationArrow arrow = arrowObject.GetComponent<CalendarNavigationArrow>();
        if (arrow == null)
            arrow = arrowObject.AddComponent<CalendarNavigationArrow>();
        arrow.Configure(this, direction);
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void OnDisable()
    {
        SetArrowObjectsVisible(false);
        if (activeCalendarController == this)
            activeCalendarController = null;
        isOpen = false;
        inputReady = false;
        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleOverridden)
            return;

        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        timeScaleOverridden = false;
    }
}
