using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
    private readonly List<SpriteRenderer> phoneGlowRenderers = new List<SpriteRenderer>();
    private readonly List<Color> phoneGlowBaseColors = new List<Color>();

    private void Awake()
    {
        ResolveReferences();
        CachePhoneDefaultPosition();
        CachePhoneGlowRenderers();
        ApplyState(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CachePhoneDefaultPosition();
        CachePhoneGlowRenderers();
        Subscribe();
        ApplyState(day1FlowController != null ? day1FlowController.State : Day1FlowState.None);
    }

    private void Update()
    {
        if (isPhoneRinging && phoneCallTransform != null && Time.unscaledTime >= nextPhoneVibrationTime)
        {
            phoneVibrationOffset = Random.insideUnitCircle * phoneVibrationAmplitude;
            nextPhoneVibrationTime = Time.unscaledTime + 1f / phoneVibrationFrequency;
        }

        if (isPhoneRinging && phoneCallTransform != null)
            phoneCallTransform.localPosition = phoneCallDefaultLocalPosition + (Vector3)phoneVibrationOffset;

        if (isPhoneGlowPulsing)
            ApplyPhoneGlowPulse();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetPhoneRinging(false);
        isPhoneGlowPulsing = false;
        SetActive(cctvGlitch, true);
        SetActive(cctvBlack, false);
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
    }

    private void SetPhoneRinging(bool ringing)
    {
        isPhoneRinging = ringing;
        nextPhoneVibrationTime = 0f;

        if (!ringing && phoneCallTransform != null)
        {
            phoneVibrationOffset = Vector2.zero;
            phoneCallTransform.localPosition = phoneCallDefaultLocalPosition;
        }

        RefreshPhoneGlow();
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
        RefreshMissedApproachVisuals();
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
        }
    }

    private void Unsubscribe()
    {
        UnsubscribeFlowController();

        if (dayRuntimeController != null)
        {
            dayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
            dayRuntimeController.DayStarted -= HandleDayStarted;
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
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
