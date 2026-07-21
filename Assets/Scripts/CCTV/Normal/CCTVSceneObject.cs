using UnityEngine;

public class CCTVSceneObject : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string objectId;
    [SerializeField] private ReportTargetId targetId = ReportTargetId.None;
    [SerializeField] private string displayName;

    [Header("Rules")]
    [SerializeField] private bool canBeAnomalyTarget = true;
    [SerializeField] private bool includeInBaseline = true;

    public string ObjectId => objectId;
    public ReportTargetId TargetId => targetId;
    public string DisplayName => displayName;
    public bool CanBeAnomalyTarget => canBeAnomalyTarget;
    public bool IncludeInBaseline => includeInBaseline;
}
