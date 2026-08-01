using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Day 씬의 메인룸 위에 제목을 띄우고, 첫 입력 뒤에만 전화 브리핑을 시작합니다.
/// 이 컴포넌트는 TitleCanvas가 아닌 항상 켜져 있는 GameManager에 붙이십시오.
/// </summary>
public class DayTitleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController dayFlowController;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private GameObject titleCanvasRoot;
    [Tooltip("타이틀 중 숨길 날짜/상황/행동 UI 루트들입니다.")]
    [SerializeField] private GameObject[] hiddenWhileTitle;

    [Header("Input")]
    [SerializeField] private bool waitForPlayerStart = true;
    [SerializeField] private bool allowAnyKeyStart = true;
    [SerializeField] private bool allowMouseClickStart = true;

    [Header("Start Presentation")]
    [Tooltip("비워두면 Title Canvas 아래의 모든 TMP 텍스트(제목, 시작 안내)를 페이드합니다.")]
    [SerializeField] private TMP_Text[] titleFadeTexts;
    [SerializeField, Min(0f)] private float titleFadeDuration = 0.45f;
    [Tooltip("타이틀이 완전히 사라진 뒤 전화가 울리기까지의 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float phoneStartDelay = 1f;

    public bool WaitForPlayerStart => waitForPlayerStart && !hasStarted;
    public bool HasStarted => hasStarted;
    /// <summary>타이틀 클릭이 메인룸 Collider까지 전달되지 않도록 하는 전역 입력 차단 상태입니다.</summary>
    public static bool IsBlockingWorldInteractions { get; private set; }

    private bool hasStarted;
    private Coroutine beginDayRoutine;
    private readonly List<Color> titleTextOriginalColors = new List<Color>();

    private void Awake()
    {
        ResolveReferences();
        CacheTitleFadeTexts();

        // 결과/일시정지 메뉴의 다시하기는 같은 Day를 즉시 재시작하므로 타이틀 입력 대기를 다시 보여주지 않는다.
        if (DayProgressSave.ConsumeSkipTitleOnNextSceneLoad())
            waitForPlayerStart = false;

        IsBlockingWorldInteractions = waitForPlayerStart;
        ApplyTitleVisibility(waitForPlayerStart);
    }

    private void Update()
    {
        if (!WaitForPlayerStart)
            return;

        bool receivedStartInput = (allowAnyKeyStart && Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape)) ||
                                  (allowMouseClickStart && Input.GetMouseButtonDown(0));
        if (receivedStartInput)
            BeginDay();
    }

    /// <summary>TitleCanvas의 Button OnClick에도 연결할 수 있습니다.</summary>
    public void BeginDay()
    {
        if (hasStarted)
            return;

        hasStarted = true;
        IsBlockingWorldInteractions = true;
        beginDayRoutine = StartCoroutine(BeginDayRoutine());
    }

    private IEnumerator BeginDayRoutine()
    {
        yield return FadeOutTitleTexts();

        if (titleCanvasRoot != null)
            titleCanvasRoot.SetActive(false);

        if (phoneStartDelay > 0f)
            yield return new WaitForSecondsRealtime(phoneStartDelay);

        SetHiddenUiVisible(true);

        int day = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : dayFlowController != null ? dayFlowController.DayNumber : 1;
        DayProgressSave.SetCurrentDay(day);
        dayFlowController?.BeginDayBriefing();
        IsBlockingWorldInteractions = false;
        beginDayRoutine = null;
    }

    private void ApplyTitleVisibility(bool titleVisible)
    {
        if (titleCanvasRoot != null)
            titleCanvasRoot.SetActive(titleVisible);

        SetHiddenUiVisible(!titleVisible);
    }

    private void SetHiddenUiVisible(bool visible)
    {
        if (hiddenWhileTitle == null)
            return;

        foreach (GameObject uiRoot in hiddenWhileTitle)
        {
            if (uiRoot != null)
                uiRoot.SetActive(visible);
        }
    }

    private IEnumerator FadeOutTitleTexts()
    {
        if (titleFadeTexts == null || titleFadeTexts.Length == 0)
            yield break;

        float duration = Mathf.Max(0.01f, titleFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyTitleTextAlpha(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        ApplyTitleTextAlpha(0f);
    }

    private void CacheTitleFadeTexts()
    {
        if ((titleFadeTexts == null || titleFadeTexts.Length == 0) && titleCanvasRoot != null)
            titleFadeTexts = titleCanvasRoot.GetComponentsInChildren<TMP_Text>(true);

        titleTextOriginalColors.Clear();
        if (titleFadeTexts == null)
            return;

        foreach (TMP_Text text in titleFadeTexts)
            titleTextOriginalColors.Add(text != null ? text.color : Color.white);
    }

    private void ApplyTitleTextAlpha(float multiplier)
    {
        if (titleFadeTexts == null)
            return;

        for (int i = 0; i < titleFadeTexts.Length; i++)
        {
            TMP_Text text = titleFadeTexts[i];
            if (text == null)
                continue;

            Color original = i < titleTextOriginalColors.Count ? titleTextOriginalColors[i] : text.color;
            original.a *= multiplier;
            text.color = original;
        }
    }

    private void ResolveReferences()
    {
        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
    }

    private void OnDisable()
    {
        if (beginDayRoutine != null)
            StopCoroutine(beginDayRoutine);
        beginDayRoutine = null;
        IsBlockingWorldInteractions = false;
    }
}
