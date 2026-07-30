using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CCTVTestSceneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayDefinition dayDefinition;
    [SerializeField] private CCTVAreaDefinition fallbackArea;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVPanController panController;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private CCTVScreenEffectController screenEffectController;
    [SerializeField] private DayRuntimeController dayRuntimeController;

    [Header("Channel Transition")]
    [SerializeField] private CCTVNoiseProfile channelSwitchNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackChannelTransitionNoiseDuration = 0.5f;
    [SerializeField, Min(0f)] private float channelSwitchDelay = 0.15f;

    [Header("Missed Approach Channel")]
    [Tooltip("미보고 누적 시 해금할 제어실 외부 고정 CCTV 구역입니다.")]
    [SerializeField] private CCTVAreaDefinition controlRoomExteriorArea;
    [SerializeField, Min(1)] private int controlRoomExteriorUnlockMissedCount = 2;
    [SerializeField] private bool selectControlRoomExteriorOnUnlock = true;
    [Tooltip("미보고 3회 침입 시 다른 채널을 대체할 제어실 내부 CCTV 구역입니다.")]
    [SerializeField] private CCTVAreaDefinition cctvRoomArea;
    [SerializeField, Min(1)] private int cctvRoomTakeoverMissedCount = 3;

    [Header("Debug")]
    [SerializeField] private AnomalyDefinition[] testAnomalies;

    private readonly List<CCTVChannelRuntime> channels = new List<CCTVChannelRuntime>();
    private int currentChannelIndex;
    private bool isSwitchingChannel;
    private bool cctvInputEnabled = true;
    private bool controlRoomExteriorUnlocked;
    private bool cctvRoomTakenOver;

    public System.Action<CCTVChannelRuntime> ChannelSelected;
    public IReadOnlyList<CCTVChannelRuntime> Channels => channels;
    public DayDefinition CurrentDayDefinition => dayDefinition;
    public bool CCTVInputEnabled => cctvInputEnabled;
    public CCTVChannelRuntime CurrentChannel =>
        channels.Count == 0 ? null : channels[currentChannelIndex];

    private void Awake()
    {
        if (areaView == null)
            areaView = FindObjectOfType<CCTVAreaView>();

        if (panController == null)
            panController = FindObjectOfType<CCTVPanController>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();

        if (screenEffectController == null)
            screenEffectController = FindObjectOfType<CCTVScreenEffectController>();

        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();
    }

    private void OnEnable()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (dayRuntimeController != null)
        {
            dayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
            dayRuntimeController.MissedAnomalyRegistered += HandleMissedAnomalyRegistered;
        }
    }

    private void OnDisable()
    {
        if (dayRuntimeController != null)
            dayRuntimeController.MissedAnomalyRegistered -= HandleMissedAnomalyRegistered;
    }

    private void Start()
    {
        BuildChannels();

        if (areaView != null)
            areaView.PrepareAreas(channels.Select(channel => channel.Area));

        SelectChannelImmediate(0);

        if (UsesMissedEscalationChannels && dayRuntimeController != null &&
            dayRuntimeController.MissedAnomalyCount >= cctvRoomTakeoverMissedCount)
        {
            TryTakeOverWithCCTVRoom();
        }
        else if (dayRuntimeController != null &&
                 dayRuntimeController.MissedAnomalyCount >= controlRoomExteriorUnlockMissedCount)
        {
            TryUnlockControlRoomExterior();
        }
    }

    private void Update()
    {
        if (!cctvInputEnabled)
            return;

        if (Input.GetKeyDown(KeyCode.Q))
            SelectPreviousChannel();

        if (Input.GetKeyDown(KeyCode.E))
            SelectNextChannel();

        // 유니티 에디터용 테스트 환경
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ActivateTestAnomaly(0);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            ActivateTestAnomaly(1);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            ActivateTestAnomaly(2);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            ActivateTestAnomaly(3);
        if (Input.GetKeyDown(KeyCode.N) && anomalyService != null)
            anomalyService.RestoreCurrentAreaBaseline();
#endif
    }

    public void SelectChannel(int index)
    {
        if (channels.Count == 0)
            return;

        if (!Application.isPlaying)
        {
            SelectChannelImmediate(index);
            return;
        }

        StartCoroutine(SwitchChannelRoutine(index));
    }

    public void SetCCTVInputEnabled(bool enabled)
    {
        cctvInputEnabled = enabled;
    }

    /// <summary>
    /// 미보고 접근 2단계에서 제어실 외부를 새 채널로 추가합니다.
    /// 성공하면 현재 채널 다음 번호(CCTV_04)로 생성되며, 옵션에 따라 즉시 전환합니다.
    /// </summary>
    public bool TryUnlockControlRoomExterior()
    {
        if (controlRoomExteriorUnlocked)
            return true;

        if (controlRoomExteriorArea == null)
        {
            Debug.LogWarning("[CCTV] Control Room Exterior Area is missing. Channel unlock skipped.");
            return false;
        }

        int existingIndex = channels.FindIndex(channel =>
            channel != null && channel.Area != null && channel.Area.AreaId == controlRoomExteriorArea.AreaId);
        if (existingIndex >= 0)
        {
            controlRoomExteriorUnlocked = true;
            if (selectControlRoomExteriorOnUnlock)
                SelectChannel(existingIndex);
            return true;
        }

        if (areaView != null && !areaView.TryPrepareAdditionalArea(controlRoomExteriorArea))
        {
            Debug.LogWarning("[CCTV] Failed to prepare Control Room Exterior area instance.");
            return false;
        }

        channels.Add(new CCTVChannelRuntime(channels.Count + 1, controlRoomExteriorArea));
        controlRoomExteriorUnlocked = true;
        int unlockedIndex = channels.Count - 1;
        Debug.Log($"[CCTV] Control Room Exterior unlocked. channel={channels[unlockedIndex].ChannelLabel}");

        if (selectControlRoomExteriorOnUnlock)
            SelectChannel(unlockedIndex);

        return true;
    }

    /// <summary>
    /// 미보고 3회 침입 단계: 기존 채널을 모두 제거하고 제어실 내부 영상 하나만 남깁니다.
    /// </summary>
    public bool TryTakeOverWithCCTVRoom()
    {
        if (cctvRoomTakenOver)
            return true;

        if (cctvRoomArea == null)
        {
            Debug.LogWarning("[CCTV] CCTV Room Area is missing. Takeover skipped.");
            return false;
        }

        if (areaView != null && !areaView.TryPrepareAdditionalArea(cctvRoomArea))
        {
            Debug.LogWarning("[CCTV] Failed to prepare CCTV Room area instance.");
            return false;
        }

        cctvRoomTakenOver = true;
        cctvInputEnabled = false;
        channels.Clear();
        channels.Add(new CCTVChannelRuntime(1, cctvRoomArea));
        currentChannelIndex = 0;
        SelectChannelImmediate(0);
        if (panController != null)
            panController.SetInputLocked(true);

        Debug.Log("[CCTV] All channels taken over by CCTV Room.");
        return true;
    }

    public void SelectNextChannel()
    {
        if (channels.Count == 0)
            return;

        int nextIndex = (currentChannelIndex + 1) % channels.Count;
        SelectChannel(nextIndex);
    }

    public void SelectPreviousChannel()
    {
        if (channels.Count == 0)
            return;

        int previousIndex = (currentChannelIndex - 1 + channels.Count) % channels.Count;
        SelectChannel(previousIndex);
    }

    private IEnumerator SwitchChannelRoutine(int index)
    {
        if (isSwitchingChannel || channels.Count == 0)
            yield break;

        int targetIndex = Mathf.Clamp(index, 0, channels.Count - 1);
        if (targetIndex == currentChannelIndex)
            yield break;

        isSwitchingChannel = true;

        if (panController != null)
            panController.SetInputLocked(true);

        CCTVScreenEffectController effectController = GetScreenEffectController();
        float noiseDuration = GetChannelSwitchNoiseDuration();
        if (effectController != null)
        {
            if (channelSwitchNoiseProfile != null)
                effectController.PlayNoise(channelSwitchNoiseProfile);
            else
                effectController.PlayTransitionNoise(fallbackChannelTransitionNoiseDuration);
        }

        float actualDelay = Mathf.Min(channelSwitchDelay, noiseDuration);
        if (actualDelay > 0f)
            yield return new WaitForSecondsRealtime(actualDelay);

        SelectChannelImmediate(targetIndex);

        float remainingDelay = Mathf.Max(0f, noiseDuration - actualDelay);
        if (remainingDelay > 0f)
            yield return new WaitForSecondsRealtime(remainingDelay);

        if (panController != null)
            panController.SetInputLocked(false);

        isSwitchingChannel = false;
    }

    private void SelectChannelImmediate(int index)
    {
        if (channels.Count == 0)
            return;

        currentChannelIndex = Mathf.Clamp(index, 0, channels.Count - 1);
        CCTVAreaDefinition area = channels[currentChannelIndex].Area;

        if (areaView != null)
            areaView.ShowArea(area);

        if (panController != null)
        {
            if (areaView != null && areaView.CurrentInstance != null)
                panController.SetAreaInstance(areaView.CurrentInstance);
            else
                panController.SetArea(area);
        }

        NotifyCCTVAreaChanged(area);
        ChannelSelected?.Invoke(channels[currentChannelIndex]);
        Debug.Log($"[CCTV] Selected {channels[currentChannelIndex].ChannelLabel} - {area.DisplayName} ({area.AreaId})");
    }

    private void NotifyCCTVAreaChanged(CCTVAreaDefinition area)
    {
        if (GameManager.Instance == null || area == null)
            return;

        string areaName = string.IsNullOrWhiteSpace(area.DisplayName)
            ? CCTVReportLabelProvider.GetAreaLabel(area.AreaId)
            : area.DisplayName;

        GameManager.Instance.NotifyCCTVAreaChanged(
            channels[currentChannelIndex].ChannelLabel,
            areaName
        );
    }

    private void BuildChannels()
    {
        channels.Clear();

        if (dayDefinition != null && dayDefinition.ActiveAreas != null)
        {
            foreach (CCTVAreaDefinition area in dayDefinition.ActiveAreas)
            {
                if (area == null)
                    continue;

                // 제어실 외부는 DayDefinition에 등록되어 있어도 미보고 2회 전까지는
                // 일반 감시 채널로 만들지 않는다.
                if (UsesMissedEscalationChannels && (IsControlRoomExteriorArea(area) || IsCCTVRoomArea(area)))
                    continue;

                channels.Add(new CCTVChannelRuntime(channels.Count + 1, area));
            }
        }

        if (channels.Count == 0 && fallbackArea != null)
            channels.Add(new CCTVChannelRuntime(1, fallbackArea));
    }

    private void HandleMissedAnomalyRegistered(AnomalyRuntime runtime)
    {
        if (!UsesMissedEscalationChannels || dayRuntimeController == null)
            return;

        if (dayRuntimeController.MissedAnomalyCount >= cctvRoomTakeoverMissedCount)
        {
            TryTakeOverWithCCTVRoom();
            return;
        }

        if (dayRuntimeController.MissedAnomalyCount < controlRoomExteriorUnlockMissedCount)
            return;

        TryUnlockControlRoomExterior();
    }

    private bool IsControlRoomExteriorArea(CCTVAreaDefinition area)
    {
        return area != null &&
               (area == controlRoomExteriorArea || area.AreaId == AreaId.ControlRoomExterior);
    }

    private bool UsesMissedEscalationChannels =>
        dayDefinition != null && dayDefinition.EnableMissedEscalationChannels;

    private bool IsCCTVRoomArea(CCTVAreaDefinition area)
    {
        return area != null &&
               (area == cctvRoomArea || area.AreaId == AreaId.CCTVRoom);
    }

    private float GetChannelSwitchNoiseDuration()
    {
        return channelSwitchNoiseProfile != null
            ? channelSwitchNoiseProfile.TotalTimedDuration
            : fallbackChannelTransitionNoiseDuration;
    }

    private void ActivateTestAnomaly(int index)
    {
        if (anomalyService == null || testAnomalies == null || index < 0 || index >= testAnomalies.Length)
            return;

        anomalyService.Activate(testAnomalies[index]);
    }

    private CCTVScreenEffectController GetScreenEffectController()
    {
        if (screenEffectController != null)
            return screenEffectController;

        screenEffectController = FindObjectOfType<CCTVScreenEffectController>();
        return screenEffectController;
    }
}




