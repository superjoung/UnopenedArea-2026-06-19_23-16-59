using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnomalyDefinition", menuName = "Unrecorded Area/Anomaly Definition")]
public class AnomalyDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string anomalyId;
    [SerializeField] private AreaId areaId = AreaId.None;

    [Header("Report Answer")]
    [SerializeField] private ReportTargetId reportTargetId = ReportTargetId.None;
    [SerializeField] private AnomalyReportType reportType = AnomalyReportType.None;

    [Header("Timing")]
    [SerializeField] private float warningDurationSec;
    [SerializeField] private float activeDurationSec = 20f;

    [Header("Progression")]
    [SerializeField] private bool canEscalate;
    [SerializeField] private AnomalyDefinition nextStage;
    [SerializeField] private bool isEmergency;

    [Header("Presentation Actions")]
    [SerializeField] private AnomalyAction[] actions;

    public string AnomalyId => anomalyId;
    public AreaId AreaId => areaId;
    public ReportTargetId ReportTargetId => reportTargetId;
    public AnomalyReportType ReportType => reportType;
    public float WarningDurationSec => Mathf.Max(0f, warningDurationSec);
    public float ActiveDurationSec => Mathf.Max(0f, activeDurationSec);
    public bool CanEscalate => canEscalate;
    public AnomalyDefinition NextStage => nextStage;
    public bool IsEmergency => isEmergency;
    public IReadOnlyList<AnomalyAction> Actions => actions ?? new AnomalyAction[0];
}
