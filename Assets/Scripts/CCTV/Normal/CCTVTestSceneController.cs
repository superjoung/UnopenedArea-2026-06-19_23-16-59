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

    [Header("Channel Transition")]
    [SerializeField] private CCTVNoiseProfile channelSwitchNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackChannelTransitionNoiseDuration = 0.5f;
    [SerializeField, Min(0f)] private float channelSwitchDelay = 0.15f;

    [Header("Debug")]
    [SerializeField] private AnomalyDefinition[] testAnomalies;

    private readonly List<CCTVChannelRuntime> channels = new List<CCTVChannelRuntime>();
    private int currentChannelIndex;
    private bool isSwitchingChannel;
    private bool cctvInputEnabled = true;

    public System.Action<CCTVChannelRuntime> ChannelSelected;
    public IReadOnlyList<CCTVChannelRuntime> Channels => channels;
    public DayDefinition CurrentDayDefinition => dayDefinition;
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
    }

    private void Start()
    {
        BuildChannels();

        if (areaView != null)
            areaView.PrepareAreas(channels.Select(channel => channel.Area));

        SelectChannelImmediate(0);
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

                channels.Add(new CCTVChannelRuntime(channels.Count + 1, area));
            }
        }

        if (channels.Count == 0 && fallbackArea != null)
            channels.Add(new CCTVChannelRuntime(1, fallbackArea));
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




