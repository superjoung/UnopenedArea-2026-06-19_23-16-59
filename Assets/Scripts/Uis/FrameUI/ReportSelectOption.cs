public enum ReportSelectKind
{
    Area,
    Object,
    AnomalyType,
}

public struct ReportSelectOption
{
    public ReportSelectKind Kind;
    public string Label;
    public AreaId AreaId;
    public string ObjectId;
    public ReportTargetId TargetId;
    public AnomalyReportType ReportType;
}
