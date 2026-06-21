public sealed class CCTVChannelRuntime
{
    public int ChannelIndex { get; }
    public string ChannelLabel { get; }
    public CCTVAreaDefinition Area { get; }

    public CCTVChannelRuntime(int channelIndex, CCTVAreaDefinition area)
    {
        ChannelIndex = channelIndex;
        ChannelLabel = $"CCTV_{channelIndex:00}";
        Area = area;
    }
}
