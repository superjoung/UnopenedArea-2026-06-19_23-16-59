using System.Collections.Generic;
using UnityEngine;

public class CCTVTestSceneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayDefinition dayDefinition;
    [SerializeField] private CCTVAreaDefinition fallbackArea;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVPanController panController;

    private readonly List<CCTVChannelRuntime> channels = new List<CCTVChannelRuntime>();
    private int currentChannelIndex;

    public IReadOnlyList<CCTVChannelRuntime> Channels => channels;
    public CCTVChannelRuntime CurrentChannel =>
        channels.Count == 0 ? null : channels[currentChannelIndex];

    private void Awake()
    {
        if (areaView == null)
            areaView = FindObjectOfType<CCTVAreaView>();

        if (panController == null)
            panController = FindObjectOfType<CCTVPanController>();
    }

    private void Start()
    {
        BuildChannels();
        SelectChannel(0);
    }

    public void SelectChannel(int index)
    {
        if (channels.Count == 0)
            return;

        currentChannelIndex = Mathf.Clamp(index, 0, channels.Count - 1);
        CCTVAreaDefinition area = channels[currentChannelIndex].Area;

        if (areaView != null)
            areaView.Bind(area);

        if (panController != null)
            panController.SetArea(area);

        Debug.Log($"[CCTV] Selected {channels[currentChannelIndex].ChannelLabel} - {area.DisplayName} ({area.AreaId})");
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
}
