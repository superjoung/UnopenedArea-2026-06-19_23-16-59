using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day 1 진행 상태에 따라 메인룸 오브젝트의 상태 연출을 제어합니다.
/// 전화 대기에는 Glow/진동을, 정전에는 CCTVBlack을 사용합니다.
/// </summary>
public class MainRoomStateEffectController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private GameObject phoneGlow;
    [SerializeField] private Transform phoneCallTransform;
    [SerializeField] private GameObject cctvGlitch;
    [SerializeField] private GameObject cctvBlack;

    [Header("Terminal Failure")]
    [Tooltip("오보고 또는 미보고 최종 실패 후 메인룸 배경 위에 표시할 이펙트입니다.")]
    [SerializeField] private GameObject terminalFailureEffect;
    [Tooltip("실패 이펙트 아래에서 메인룸을 완전히 가릴 검은 배경입니다.")]
    [SerializeField] private GameObject terminalFailureBackground;

    [Header("Blackout Main Room")]
    [SerializeField] private GameObject normalBackground;
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private GameObject controlSign;
    [SerializeField, Range(0f, 1f)] private float controlSignBlackoutBrightness = 0.45f;

    [Header("Blackout Interactable Dimming")]
    [Tooltip("정전 시 함께 어두워질 메인룸 상호작용 오브젝트입니다. 비워두면 CCTV, Phone, Door, Report를 자동 연결합니다.")]
    [SerializeField] private GameObject[] blackoutDimObjects = new GameObject[4];
    [Tooltip("#FFFFFF 기준 #A4A4A4는 약 0.64입니다.")]
    [SerializeField, Range(0f, 1f)] private float blackoutInteractableBrightness = 0.643f;

    [Header("Calendar")]
    [Tooltip("0=Day 1, 1=Day 2, 2=Day 3. 각 날짜 스프라이트 오브젝트를 넣습니다.")]
    [SerializeField] private GameObject[] calendarDaySprites = new GameObject[3];

    [Header("Missed Approach - First Pass")]
    [Tooltip("Report/WordCanvas 아래의 Wrong TMP 텍스트입니다. 미보고가 없을 때는 숨깁니다.")]
    [SerializeField] private TMP_Text missedApproachText;
    [Tooltip("다음 단계에서 2회 미보고 접근 연출에 사용할 Door 자식 Shadow입니다.")]
    [SerializeField] private GameObject doorShadow;

    [Header("Phone Vibration")]
    [Tooltip("전화가 흔들리는 반경입니다. X/Y 양방향으로 적용됩니다.")]
    [SerializeField, Min(0f)] private float phoneVibrationAmplitude = 0.012f;
    [Tooltip("초당 흔들림 위치 변경 횟수입니다.")]
    [SerializeField, Min(0.1f)] private float phoneVibrationFrequency = 45f;
    [Tooltip("한 번의 전화 진동이 유지되는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float phoneVibrationOnDuration = 1f;
    [Tooltip("다음 진동 전, 전화기가 멈춰 있는 시간입니다.")]
    [SerializeField, Min(0f)] private float phoneVibrationOffDuration = 0.5f;

    [Header("Phone Glow Pulse")]
    [SerializeField, Range(0f, 1f)] private float phoneGlowMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float phoneGlowMaxAlpha = 1f;
    [SerializeField, Min(0.05f)] private float phoneGlowFadeDuration = 1f;

    private Day1FlowController subscribedFlowController;
    private Vector3 phoneCallDefaultLocalPosition;
    private bool phoneDefaultPositionCached;
    private bool isPhoneRinging;
    private bool isPhoneGlowPulsing;
    private int missedAnomalyCount;
    private Vector2 phoneVibrationOffset;
    private float nextPhoneVibrationTime;
    private float phoneVibrationPhaseEndTime;
    private bool isPhoneVibrationActive;
    private readonly List<SpriteRenderer> phoneGlowRenderers = new List<SpriteRenderer>();
    private readonly List<Color> phoneGlowBaseColors = new List<Color>();
    private readonly List<SpriteRenderer> controlSignSpriteRenderers = new List<SpriteRenderer>();
    private readonly List<Color> controlSignSpriteBaseColors = new List<Color>();
    private readonly List<TMP_Text> controlSignTexts = new List<TMP_Text>();
    private readonly List<Color> controlSignTextBaseColors = new List<Color>();
    private readonly List<Graphic> controlSignGraphics = new List<Graphic>();
    private readonly List<Color> controlSignGraphicBaseColors = new List<Color>();
    private readonly List<SpriteRenderer> blackoutDimRenderers = new List<SpriteRenderer>();
    private readonly List<Color> blackoutDimBaseColors = new List<Color>();

    private void Awake()
    {
        ResolveReferences();
        CachePhoneDefaultPosition();
        CachePhoneGlowRenderers();
        CacheControlSignColors();
        CacheBlackoutDimColors();
        RefreshCalendar();
        SetActive(terminalFailureEffect, false);
        SetActive(terminalFailureBackground, false);
        ApplyState(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CachePhoneDefaultPosition();
        CachePhoneGlowRenderers();
        CacheControlSignColors();
        CacheBlackoutDimColors();
        RefreshCalendar();
        Subscribe();
        ApplyState(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
    }

    private void Update()
    {
        UpdatePhoneVibrationPhase();

        if (isPhoneRinging && isPhoneVibrationActive && phoneCallTransform != null && Time.unscaledTime >= nextPhoneVibrationTime)
        {
            phoneVibrationOffset = Random.insideUnitCircle * phoneVibrationAmplitude;
            nextPhoneVibrationTime = Time.unscaledTime + 1f / phoneVibrationFrequency;
        }

        if (isPhoneRinging && isPhoneVibrationActive && phoneCallTransform != null)
            phoneCallTransform.localPosition = phoneCallDefaultLocalPosition + (Vector3)phoneVibrationOffset;

        if (isPhoneGlowPulsing)
            ApplyPhoneGlowPulse();
    }

    private void OnDisable()
    {
        SetPhoneRinging(false);
        isPhoneGlowPulsing = false;
        SetActive(cctvGlitch, true);
        SetActive(cctvBlack, false);
        SetActive(normalBackground, true);
        SetActive(darkBackground, false);
        SetActive(terminalFailureEffect, false);
        SetActive(terminalFailureBackground, false);
        ApplyControlSignBrightness(1f);
        ApplyBlackoutDimBrightness(1f);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void HandleStateChanged(Day1FlowState state)
    {
        ApplyState(state);
    }

    private void ApplyState(Day1FlowState state)
    {
        SetPhoneRinging(state == Day1FlowState.Briefing);
        RefreshMissedApproachVisuals();
        // 정전 전환 중에는 아직 State가 Monitoring이지만 Emergency 프레젠테이션 잠금이 이미 시작된다.
        // 메인룸이 검은 화면 아래에서 켜질 때 CCTVBlack이 먼저 보이도록 이 구간도 정전으로 취급한다.
        bool isBlackout = state == Day1FlowState.EmergencyDispatch ||
                          (state == Day1FlowState.Monitoring &&
                           day1FlowController != null && day1FlowController.IsEmergencyPresentationLocked);
        SetActive(cctvGlitch, !isBlackout);
        SetActive(cctvBlack, isBlackout);
        SetActive(normalBackground, !isBlackout);
        SetActive(darkBackground, isBlackout);
        ApplyControlSignBrightness(isBlackout ? controlSignBlackoutBrightness : 1f);
        ApplyBlackoutDimBrightness(isBlackout ? blackoutInteractableBrightness : 1f);
    }

    private void SetPhoneRinging(bool ringing)
    {
        isPhoneRinging = ringing;
        nextPhoneVibrationTime = 0f;
        isPhoneVibrationActive = ringing;
        phoneVibrationPhaseEndTime = ringing
            ? Time.unscaledTime + phoneVibrationOnDuration
            : 0f;

        if (!ringing && phoneCallTransform != null)
        {
            phoneVibrationOffset = Vector2.zero;
            phoneCallTransform.localPosition = phoneCallDefaultLocalPosition;
        }

        if (ringing)
            SoundManager.Instance?.PlayPhoneRingSfx();

        RefreshPhoneGlow();
    }

    private void UpdatePhoneVibrationPhase()
    {
        if (!isPhoneRinging || Time.unscaledTime < phoneVibrationPhaseEndTime)
            return;

        isPhoneVibrationActive = !isPhoneVibrationActive;
        phoneVibrationPhaseEndTime = Time.unscaledTime +
            (isPhoneVibrationActive ? phoneVibrationOnDuration : phoneVibrationOffDuration);

        if (isPhoneVibrationActive)
            SoundManager.Instance?.PlayPhoneRingSfx();

        if (!isPhoneVibrationActive && phoneCallTransform != null)
        {
            phoneVibrationOffset = Vector2.zero;
            phoneCallTransform.localPosition = phoneCallDefaultLocalPosition;
        }
    }

    private void HandleMissedAnomalyRegistered(AnomalyRuntime runtime)
    {
        missedAnomalyCount = dayRuntimeController != null
            ? dayRuntimeController.MissedAnomalyCount
            : missedAnomalyCount + 1;
        RefreshMissedApproachVisuals();
    }

    private void HandleDayStarted()
    {
        missedAnomalyCount = 0;
        SetActive(terminalFailureEffect, false);
        SetActive(terminalFailureBackground, false);
        RefreshMissedApproachVisuals();
        RefreshCalendar();
    }

    private void HandleTerminalFailureStarted(DayFailureReason reason)
    {
        SetActive(terminalFailureEffect, false);
        SetActive(terminalFailureBackground, false);
    }

    public void RevealTerminalFailureEffect()
    {
        SetActive(terminalFailureBackground, true);
        SetActive(terminalFailureEffect, true);
    }

    private void RefreshMissedApproachVisuals()
    {
        if (dayRuntimeController != null)
            missedAnomalyCount = dayRuntimeController.MissedAnomalyCount;

        if (missedApproachText != null)
        {
            bool shouldShow = missedAnomalyCount > 0;
            missedApproachText.gameObject.SetActive(shouldShow);
            if (shouldShow)
                missedApproachText.text = GetMissedApproachMessage(missedAnomalyCount);
        }

        // 이번 단계에서는 그림자를 등록만 한다. 2회 미보고 연출에서 활성화한다.
        SetActive(doorShadow, false);
        RefreshPhoneGlow();
    }

    private void RefreshPhoneGlow()
    {
        isPhoneGlowPulsing = isPhoneRinging || missedAnomalyCount > 0;
        SetActive(phoneGlow, isPhoneGlowPulsing);

        if (!isPhoneGlowPulsing)
            RestorePhoneGlowColors();
    }

    private static string GetMissedApproachMessage(int count)
    {
        switch (Mathf.Clamp(count, 1, 3))
        {
            case 1: return "점점 다가오고 있어...";
            case 2: return "거의 다 왔다...";
            default: return "찾았다.";
        }
    }

    private void CachePhoneDefaultPosition()
    {
        if (phoneDefaultPositionCached || phoneCallTransform == null)
            return;

        phoneCallDefaultLocalPosition = phoneCallTransform.localPosition;
        phoneDefaultPositionCached = true;
    }

    private void CachePhoneGlowRenderers()
    {
        if (phoneGlow == null || phoneGlowRenderers.Count > 0)
            return;

        foreach (SpriteRenderer renderer in phoneGlow.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null)
                continue;

            phoneGlowRenderers.Add(renderer);
            phoneGlowBaseColors.Add(renderer.color);
        }
    }

    private void CacheControlSignColors()
    {
        if (controlSign == null || controlSignSpriteRenderers.Count > 0 ||
            controlSignTexts.Count > 0 || controlSignGraphics.Count > 0)
            return;

        foreach (SpriteRenderer renderer in controlSign.GetComponentsInChildren<SpriteRenderer>(true))
        {
            controlSignSpriteRenderers.Add(renderer);
            controlSignSpriteBaseColors.Add(renderer.color);
        }

        foreach (TMP_Text text in controlSign.GetComponentsInChildren<TMP_Text>(true))
        {
            controlSignTexts.Add(text);
            controlSignTextBaseColors.Add(text.color);
        }

        foreach (Graphic graphic in controlSign.GetComponentsInChildren<Graphic>(true))
        {
            controlSignGraphics.Add(graphic);
            controlSignGraphicBaseColors.Add(graphic.color);
        }
    }

    private void ApplyControlSignBrightness(float brightness)
    {
        brightness = Mathf.Clamp01(brightness);
        CacheControlSignColors();

        for (int i = 0; i < controlSignSpriteRenderers.Count; i++)
            if (controlSignSpriteRenderers[i] != null)
                controlSignSpriteRenderers[i].color = MultiplyRgb(controlSignSpriteBaseColors[i], brightness);

        for (int i = 0; i < controlSignTexts.Count; i++)
            if (controlSignTexts[i] != null)
                controlSignTexts[i].color = MultiplyRgb(controlSignTextBaseColors[i], brightness);

        for (int i = 0; i < controlSignGraphics.Count; i++)
            if (controlSignGraphics[i] != null)
                controlSignGraphics[i].color = MultiplyRgb(controlSignGraphicBaseColors[i], brightness);
    }

    private void CacheBlackoutDimColors()
    {
        if (blackoutDimRenderers.Count > 0)
            return;

        foreach (GameObject target in blackoutDimObjects)
        {
            if (target == null)
                continue;

            foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || blackoutDimRenderers.Contains(renderer))
                    continue;

                blackoutDimRenderers.Add(renderer);
                blackoutDimBaseColors.Add(renderer.color);
            }
        }
    }

    private void ApplyBlackoutDimBrightness(float brightness)
    {
        CacheBlackoutDimColors();
        brightness = Mathf.Clamp01(brightness);

        for (int i = 0; i < blackoutDimRenderers.Count; i++)
        {
            if (blackoutDimRenderers[i] != null)
                blackoutDimRenderers[i].color = MultiplyRgb(blackoutDimBaseColors[i], brightness);
        }
    }

    private static Color MultiplyRgb(Color baseColor, float brightness)
    {
        baseColor.r *= brightness;
        baseColor.g *= brightness;
        baseColor.b *= brightness;
        return baseColor;
    }

    private void RefreshCalendar()
    {
        int day = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : day1FlowController != null ? day1FlowController.DayNumber : 1;

        for (int i = 0; i < calendarDaySprites.Length; i++)
            SetActive(calendarDaySprites[i], i == day - 1);
    }

    private void ApplyPhoneGlowPulse()
    {
        float cycle = Mathf.PingPong(Time.unscaledTime / phoneGlowFadeDuration, 1f);
        float alpha = Mathf.Lerp(phoneGlowMinAlpha, phoneGlowMaxAlpha, cycle);
        for (int i = 0; i < phoneGlowRenderers.Count; i++)
        {
            SpriteRenderer renderer = phoneGlowRenderers[i];
            if (renderer == null)
                continue;

            Color color = phoneGlowBaseColors[i];
            color.a *= alpha;
            renderer.color = color;
        }
    }

    private void RestorePhoneGlowColors()
    {
        for (int i = 0; i < phoneGlowRenderers.Count; i++)
        {
            if (phoneGlowRenderers[i] != null)
                phoneGlowRenderers[i].color = phoneGlowBaseColors[i];
        }
    }

    private void Subscribe()
    {
        if (day1FlowController != null && subscribedFlowController != day1FlowController)
        {
            UnsubscribeFlowController();
            subscribedFlowController = day1FlowController;
            subscribedFlowController.StateChanged += HandleStateChanged;
        }

        if (dayRuntimeController != null)
        {
            dayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
            dayRuntimeController.MissedAnomalyRegistered += HandleMissedAnomalyRegistered;
            dayRuntimeController.DayStarted -= HandleDayStarted;
            dayRuntimeController.DayStarted += HandleDayStarted;
            dayRuntimeController.TerminalFailureStarted -= HandleTerminalFailureStarted;
            dayRuntimeController.TerminalFailureStarted += HandleTerminalFailureStarted;
        }
    }

    private void Unsubscribe()
    {
        UnsubscribeFlowController();

        if (dayRuntimeController != null)
        {
            dayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
            dayRuntimeController.DayStarted -= HandleDayStarted;
            dayRuntimeController.TerminalFailureStarted -= HandleTerminalFailureStarted;
        }
    }

    private void UnsubscribeFlowController()
    {
        if (subscribedFlowController == null)
            return;

        subscribedFlowController.StateChanged -= HandleStateChanged;
        subscribedFlowController = null;
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();

        if (missedApproachText == null)
        {
            Transform wrongTextTransform = transform.Find("Report/WordCanvas/Wrong");
            if (wrongTextTransform != null)
                missedApproachText = wrongTextTransform.GetComponent<TMP_Text>();
        }

        if (doorShadow == null)
        {
            Transform shadowTransform = transform.Find("Door/Shadow");
            if (shadowTransform != null)
                doorShadow = shadowTransform.gameObject;
        }

        if (normalBackground == null)
            normalBackground = FindChildObject("BackGround");
        if (darkBackground == null)
            darkBackground = FindChildObject("DarkBackGround");
        if (terminalFailureBackground == null)
            terminalFailureBackground = FindChildObject("TerminalFailureBackground");
        if (controlSign == null)
            controlSign = FindChildObject("Controll");

        string[] defaultDimObjectNames = { "CCTV", "Phone", "Door", "Report" };
        for (int i = 0; i < blackoutDimObjects.Length && i < defaultDimObjectNames.Length; i++)
        {
            if (blackoutDimObjects[i] == null)
                blackoutDimObjects[i] = FindChildObject(defaultDimObjectNames[i]);
        }

        Transform calendar = transform.Find("Callender");
        if (calendar != null)
        {
            for (int i = 0; i < calendarDaySprites.Length; i++)
            {
                if (calendarDaySprites[i] == null && i < calendar.childCount)
                    calendarDaySprites[i] = calendar.GetChild(i).gameObject;
            }
        }
        else
        {
            // CCTVRoom처럼 달력 3장이 루트에 직접 배치된 프리팹도 지원한다.
            for (int i = 0; i < calendarDaySprites.Length; i++)
            {
                if (calendarDaySprites[i] == null)
                    calendarDaySprites[i] = FindChildObject($"Callender{i + 1}");
            }
        }
    }

    private GameObject FindChildObject(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
