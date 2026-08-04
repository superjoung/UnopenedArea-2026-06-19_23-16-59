using System.Collections.Generic;

public static class CCTVReportLabelProvider
{
    public static string GetAreaLabel(AreaId id)
    {
        switch (id)
        {
            case AreaId.LabCorridor:
                return "연구실 복도";
            case AreaId.TreatmentRoom:
                return "처치실";
            case AreaId.UtilityRoom:
                return "관리실";
            case AreaId.ServerRoom:
                return "서버실";
            case AreaId.ControlRoomExterior:
                return "제어실 외부";
            case AreaId.CCTVRoom:
                return "제어실 내부";
            case AreaId.None:
                return "선택";
            default:
                return $"알 수 없음({id})";
        }
    }

    public static string GetTargetLabel(ReportTargetId id)
    {
        switch (id)
        {
            case ReportTargetId.Wheelchair:
                return "휠체어";
            case ReportTargetId.Chair:
                return "의자";
            case ReportTargetId.Door:
                return "문";
            case ReportTargetId.Light:
                return "조명";
            case ReportTargetId.Portrait:
                return "초상화";
            case ReportTargetId.Curtain:
                return "커튼";
            case ReportTargetId.ToolBox:
                return "공구함";
            case ReportTargetId.PatientBed:
                return "환자 침대";
            case ReportTargetId.ExitSign:
                return "비상구 표지";
            case ReportTargetId.Clock:
                return "벽시계";
            case ReportTargetId.Phone:
                return "전화기";
            case ReportTargetId.Poster:
                return "포스터";
            case ReportTargetId.Plant:
                return "화분";
            case ReportTargetId.Stand:
                return "스탠드";
            case ReportTargetId.Person:
                return "사람";
            case ReportTargetId.Monitor:
                return "모니터";
            case ReportTargetId.ServerRack:
                return "서버 랙";
            case ReportTargetId.Vent:
                return "환기구";
            case ReportTargetId.Sound:
                return "소리";
            case ReportTargetId.CCTVVideo:
                return "CCTV 화면";
            case ReportTargetId.None:
                return "선택";
            default:
                return $"알 수 없음({id})";
        }
    }

    public static string GetReportTypeLabel(AnomalyReportType type)
    {
        switch (type)
        {
            case AnomalyReportType.PositionChange:
                return "위치 변경";
            case AnomalyReportType.Added:
                return "추가됨";
            case AnomalyReportType.Missing:
                return "사라짐";
            case AnomalyReportType.StateChange:
                return "상태 변경";
            case AnomalyReportType.ShapeChange:
                return "형태 손상";
            case AnomalyReportType.AbnormalBehavior:
                return "이상 행동";
            case AnomalyReportType.None:
                return "선택";
            default:
                return $"알 수 없음({type})";
        }
    }

    public static List<string> GetAreaLabels(IReadOnlyList<AreaId> ids)
    {
        var labels = new List<string>();
        if (ids == null)
            return labels;

        foreach (AreaId id in ids)
            labels.Add(GetAreaLabel(id));

        return labels;
    }

    public static List<string> GetTargetLabels(IReadOnlyList<ReportTargetId> ids)
    {
        var labels = new List<string>();
        if (ids == null)
            return labels;

        foreach (ReportTargetId id in ids)
            labels.Add(GetTargetLabel(id));

        return labels;
    }

    public static List<string> GetReportTypeLabels(IReadOnlyList<AnomalyReportType> types)
    {
        var labels = new List<string>();
        if (types == null)
            return labels;

        foreach (AnomalyReportType type in types)
            labels.Add(GetReportTypeLabel(type));

        return labels;
    }
}
