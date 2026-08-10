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
            case ReportTargetId.Cart:
                return "카트";
            case ReportTargetId.Switch:
                return "스위치";
            case ReportTargetId.CeilingWire:
                return "천장 전선";
            case ReportTargetId.Person:
                return "인물";
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

    /// <summary>
    /// 보고판의 현상 유형이 서로 어떻게 다른지 설명하는 짧은 판정 기준입니다.
    /// 특정 이상현상의 정답을 직접 알려주지 않고, 유형의 의미만 안내합니다.
    /// </summary>
    public static string GetReportTypeDescription(AnomalyReportType type)
    {
        switch (type)
        {
            case AnomalyReportType.PositionChange:
                return "대상이 정상 위치에서 다른 자리로 옮겨진 현상입니다.";
            case AnomalyReportType.Added:
                return "기준 화면에 없던 대상이 새로 나타난 현상입니다.";
            case AnomalyReportType.Missing:
                return "기준 화면에 있던 대상이 보이지 않게 된 현상입니다.";
            case AnomalyReportType.StateChange:
                return "대상의 자세·방향·열림과 닫힘 등 상태가 달라진 현상입니다.";
            case AnomalyReportType.ShapeChange:
                return "대상의 외형이나 모양 자체가 비정상적으로 변형·훼손된 현상입니다.";
            case AnomalyReportType.AbnormalBehavior:
                return "사람이나 물체가 이동·떨림·반복 동작 등 비정상적으로 움직이는 현상입니다.";
            case AnomalyReportType.None:
                return string.Empty;
            default:
                return "선택한 현상 유형의 판정 기준을 확인할 수 없습니다.";
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
