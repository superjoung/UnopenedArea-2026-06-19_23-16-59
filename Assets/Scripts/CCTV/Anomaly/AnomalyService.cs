using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnomalyService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CCTVAreaView areaView;

    [Header("Runtime")]
    [SerializeField] private bool timersPaused;

    private readonly List<AnomalyRuntime> activeAnomalies = new List<AnomalyRuntime>();

    public System.Action<AnomalyRuntime> AnomalyMissed;
    public System.Action<AnomalyRuntime> AnomalyResolved;
    public IReadOnlyList<AnomalyRuntime> ActiveAnomalies => activeAnomalies;
    public bool TimersPaused => timersPaused;

    private void Awake()
    {
        if (areaView == null)
            areaView = FindObjectOfType<CCTVAreaView>();
    }

    private void Update()
    {
        if (timersPaused)
            return;

        for (int i = activeAnomalies.Count - 1; i >= 0; i--)
        {
            AnomalyRuntime runtime = activeAnomalies[i];
            runtime.Tick(Time.deltaTime);

            if (runtime.IsTimedOut)
                MarkMissed(runtime);
        }
    }

    public AnomalyRuntime Activate(AnomalyDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[AnomalyService] Activate failed. definition is null.");
            return null;
        }

        AnomalyRuntime existingRuntime = FindUnresolvedRuntime(definition);
        if (existingRuntime != null)
        {
            Debug.Log($"[AnomalyService] Duplicate activation ignored. anomaly={definition.AnomalyId}, area={definition.AreaId}, state={existingRuntime.State}");
            return existingRuntime;
        }

        CCTVAreaInstance instance = GetAreaInstance(definition.AreaId);
        if (instance == null)
        {
            Debug.LogWarning($"[AnomalyService] Activate failed. area instance is missing. areaId={definition.AreaId}, anomaly={definition.AnomalyId}");
            return null;
        }

        var runtime = new AnomalyRuntime(definition);
        runtime.ChangeState(AnomalyState.Activating);
        activeAnomalies.Add(runtime);
        StartCoroutine(ActivateRoutine(runtime, instance));
        return runtime;
    }

    public void Resolve(AnomalyRuntime runtime)
    {
        if (runtime == null || !activeAnomalies.Contains(runtime))
            return;

        runtime.ChangeState(AnomalyState.Normalizing);
        RestoreAreaBaseline(runtime.Definition.AreaId);
        runtime.ChangeState(AnomalyState.Resolved);
        activeAnomalies.Remove(runtime);
        AnomalyResolved?.Invoke(runtime);
        Debug.Log($"[AnomalyService] Resolved anomaly={runtime.Definition.AnomalyId}");
    }

    public void CompleteNormalization(AnomalyRuntime runtime)
    {
        if (runtime == null || !activeAnomalies.Contains(runtime))
            return;

        if (runtime.State != AnomalyState.Normalizing)
        {
            Debug.LogWarning($"[AnomalyService] CompleteNormalization ignored. anomaly={runtime.Definition.AnomalyId}, state={runtime.State}");
            return;
        }

        Resolve(runtime);
    }

    public bool TryReportAnomaly(AreaId areaId, ReportTargetId targetId, AnomalyReportType reportType, out AnomalyRuntime matchedRuntime)
    {
        matchedRuntime = null;

        if (areaId == AreaId.None || targetId == ReportTargetId.None || reportType == AnomalyReportType.None)
        {
            Debug.LogWarning($"[AnomalyService] Report rejected. Missing report value. area={areaId}, target={targetId}, type={reportType}");
            return false;
        }

        for (int i = 0; i < activeAnomalies.Count; i++)
        {
            AnomalyRuntime runtime = activeAnomalies[i];
            if (runtime == null || runtime.Definition == null)
                continue;

            if (runtime.State != AnomalyState.Active)
                continue;

            AnomalyDefinition definition = runtime.Definition;
            if (definition.AreaId != areaId)
                continue;

            if (definition.ReportTargetId != targetId)
                continue;

            if (definition.ReportType != reportType)
                continue;

            matchedRuntime = runtime;
            runtime.ChangeState(AnomalyState.Normalizing);
            return true;
        }

        Debug.Log($"[AnomalyService] Wrong report. area={areaId}, target={targetId}, type={reportType}");
        return false;
    }

    public void RestoreCurrentAreaBaseline()
    {
        CCTVAreaInstance instance = areaView != null ? areaView.CurrentInstance : null;
        if (instance != null)
            instance.RestoreBaseline();
    }

    public void RestoreAreaBaseline(AreaId areaId)
    {
        CCTVAreaInstance instance = GetAreaInstance(areaId);
        if (instance != null)
            instance.RestoreBaseline();
    }

    public void SetTimersPaused(bool paused)
    {
        timersPaused = paused;
    }

    private AnomalyRuntime FindUnresolvedRuntime(AnomalyDefinition definition)
    {
        for (int i = 0; i < activeAnomalies.Count; i++)
        {
            AnomalyRuntime runtime = activeAnomalies[i];
            if (runtime == null || runtime.Definition == null)
                continue;

            if (runtime.State != AnomalyState.Activating && runtime.State != AnomalyState.Active)
                continue;

            if (IsSameAnomaly(runtime.Definition, definition))
                return runtime;
        }

        return null;
    }

    private bool IsSameAnomaly(AnomalyDefinition a, AnomalyDefinition b)
    {
        if (a == null || b == null)
            return false;

        if (!string.IsNullOrEmpty(a.AnomalyId) && !string.IsNullOrEmpty(b.AnomalyId))
            return a.AreaId == b.AreaId && a.AnomalyId == b.AnomalyId;

        return a == b;
    }

    private CCTVAreaInstance GetAreaInstance(AreaId areaId)
    {
        if (areaView == null)
            return null;

        return areaView.TryGetAreaInstance(areaId, out CCTVAreaInstance instance)
            ? instance
            : null;
    }

    private IEnumerator ActivateRoutine(AnomalyRuntime runtime, CCTVAreaInstance instance)
    {
        if (runtime.Definition.WarningDurationSec > 0f)
            yield return WaitForAnomalySeconds(runtime.Definition.WarningDurationSec);

        foreach (AnomalyAction action in runtime.Definition.Actions)
        {
            if (action == null)
                continue;

            if (action.DelaySec > 0f)
                yield return WaitForAnomalySeconds(action.DelaySec);

            ApplyAction(runtime.Definition, instance, action);
        }

        runtime.ActivateTimer();
        Debug.Log($"[AnomalyService] Activated anomaly={runtime.Definition.AnomalyId}, area={runtime.Definition.AreaId}, duration={runtime.RemainingActiveTimeSec}");
    }

    private IEnumerator WaitForAnomalySeconds(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            if (!timersPaused)
                remaining -= Time.deltaTime;

            yield return null;
        }
    }

    private void ApplyAction(AnomalyDefinition definition, CCTVAreaInstance instance, AnomalyAction action)
    {
        if (!instance.TryGetObject(action.TargetObjectId, out CCTVSceneObject sceneObject) || sceneObject == null)
        {
            Debug.LogWarning($"[AnomalyService] Action target missing. anomaly={definition.AnomalyId}, targetObjectId={action.TargetObjectId}");
            return;
        }

        switch (action.ActionType)
        {
            case AnomalyActionType.SetActive:
                sceneObject.gameObject.SetActive(action.ActiveValue);
                break;
            case AnomalyActionType.MoveToLocalPosition:
                sceneObject.transform.localPosition = action.TargetLocalPosition;
                break;
            case AnomalyActionType.SetLocalEulerAngles:
                sceneObject.transform.localEulerAngles = action.TargetLocalEulerAngles;
                break;
            case AnomalyActionType.MoveByLocalPositionOffset:
                sceneObject.transform.localPosition += action.LocalPositionOffset;
                break;
            case AnomalyActionType.ChangeSprite:
                ApplySprite(sceneObject, action.TargetSprite);
                break;
            case AnomalyActionType.ChangeColor:
                ApplyColor(sceneObject, action.TargetColor);
                break;
            case AnomalyActionType.None:
            default:
                Debug.LogWarning($"[AnomalyService] Unsupported action. anomaly={definition.AnomalyId}, action={action.ActionType}");
                break;
        }
    }

    private void ApplySprite(CCTVSceneObject sceneObject, Sprite sprite)
    {
        SpriteRenderer renderer = sceneObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = sprite;
    }

    private void ApplyColor(CCTVSceneObject sceneObject, Color color)
    {
        SpriteRenderer renderer = sceneObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.color = color;
    }

    private void MarkMissed(AnomalyRuntime runtime)
    {
        runtime.ChangeState(AnomalyState.Missed);
        RestoreAreaBaseline(runtime.Definition.AreaId);
        activeAnomalies.Remove(runtime);
        AnomalyMissed?.Invoke(runtime);
        Debug.Log($"[AnomalyService] Missed anomaly={runtime.Definition.AnomalyId}, area={runtime.Definition.AreaId}. Restored baseline.");
    }
}








