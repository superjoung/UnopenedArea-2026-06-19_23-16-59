using System.Collections.Generic;
using UnityEngine;

public class CCTVAreaInstance : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform propRoot;
    [SerializeField] private Transform anomalyRoot;
    [SerializeField] private Transform audioRoot;

    private readonly Dictionary<string, CCTVSceneObject> objectsById = new Dictionary<string, CCTVSceneObject>();
    private readonly Dictionary<string, CCTVObjectBaselineState> baselineByObjectId = new Dictionary<string, CCTVObjectBaselineState>();

    public CCTVAreaDefinition Definition { get; private set; }
    public Vector3 WorldOrigin { get; private set; }
    public Transform PropRoot => propRoot;
    public Transform AnomalyRoot => anomalyRoot;
    public Transform AudioRoot => audioRoot;

    public void Initialize(CCTVAreaDefinition definition)
    {
        Initialize(definition, transform.position);
    }

    public void Initialize(CCTVAreaDefinition definition, Vector3 worldOrigin)
    {
        Definition = definition;
        WorldOrigin = worldOrigin;
        EnsureRoots();
        RebuildRegistry();
        CaptureBaseline();
    }

    public float GetViewWorldMinX()
    {
        if (Definition == null)
            return WorldOrigin.x;

        return WorldOrigin.x + Definition.ViewWorldMinX;
    }

    public float GetViewWorldMaxX()
    {
        if (Definition == null)
            return WorldOrigin.x;

        return WorldOrigin.x + Definition.ViewWorldMaxX;
    }

    public float GetViewWorldCenterY()
    {
        if (Definition == null)
            return WorldOrigin.y;

        return WorldOrigin.y + Definition.AreaCenter.y;
    }

    public bool TryGetObject(string objectId, out CCTVSceneObject sceneObject)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            sceneObject = null;
            return false;
        }

        return objectsById.TryGetValue(objectId, out sceneObject);
    }

    public IReadOnlyCollection<CCTVSceneObject> GetAllObjects()
    {
        return objectsById.Values;
    }

    public void CaptureBaseline()
    {
        baselineByObjectId.Clear();

        foreach (var pair in objectsById)
        {
            CCTVSceneObject sceneObject = pair.Value;
            if (sceneObject == null || !sceneObject.IncludeInBaseline)
                continue;

            baselineByObjectId[pair.Key] = new CCTVObjectBaselineState(sceneObject);
        }
    }

    public void RestoreBaseline()
    {
        foreach (var pair in baselineByObjectId)
        {
            if (!objectsById.TryGetValue(pair.Key, out CCTVSceneObject sceneObject) || sceneObject == null)
                continue;

            pair.Value.Restore(sceneObject);
        }
    }

    private void RebuildRegistry()
    {
        objectsById.Clear();
        CCTVSceneObject[] sceneObjects = GetComponentsInChildren<CCTVSceneObject>(true);

        foreach (CCTVSceneObject sceneObject in sceneObjects)
        {
            if (sceneObject == null || string.IsNullOrWhiteSpace(sceneObject.ObjectId))
            {
                Debug.LogWarning($"[CCTVAreaInstance] Object without id skipped. area={Definition?.AreaId}, object={sceneObject?.name}");
                continue;
            }

            if (objectsById.ContainsKey(sceneObject.ObjectId))
            {
                Debug.LogWarning($"[CCTVAreaInstance] Duplicate objectId skipped. area={Definition?.AreaId}, objectId={sceneObject.ObjectId}");
                continue;
            }

            objectsById.Add(sceneObject.ObjectId, sceneObject);
        }
    }

    private void EnsureRoots()
    {
        propRoot = EnsureRoot(propRoot, "PropRoot");
        anomalyRoot = EnsureRoot(anomalyRoot, "AnomalyRoot");
        audioRoot = EnsureRoot(audioRoot, "AudioRoot");
    }

    private Transform EnsureRoot(Transform root, string rootName)
    {
        if (root != null)
            return root;

        Transform found = transform.Find(rootName);
        if (found != null)
            return found;

        var go = new GameObject(rootName);
        found = go.transform;
        found.SetParent(transform, false);
        return found;
    }
}
