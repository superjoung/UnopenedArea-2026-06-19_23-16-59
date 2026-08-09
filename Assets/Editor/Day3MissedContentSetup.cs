#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Day3 미보고 공포용 빈 슬롯과 씬 참조를 반복 실행 가능하게 구성합니다.</summary>
public static class Day3MissedContentSetup
{
    private const string ScenePath = "Assets/Scenes/Day3.unity";
    private const string PrefabRoot = "Assets/Resources/Prefabs/AreaPrefabs/";

    [MenuItem("Tools/Day3/Apply Missed Horror Setup")]
    public static void Apply()
    {
        ConfigureViewportEffect(
            "Area_LabCorridor.prefab",
            "D3_NR02_CeilingPerson",
            new Vector3(18f, 2.8f, 0f),
            "OBJ_DORM_CEILING_PERSON_01",
            "D3 NR02 Ceiling Person",
            200);

        ConfigureViewportEffect(
            "Area_TreatmentRoom.prefab",
            "D3_NR03_ReflectionPerson",
            new Vector3(0f, 1.2f, 0f),
            "OBJ_TREAT_REFLECTION_01",
            "D3 NR03 Reflection Person",
            150);

        ConfigureDay3Scene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Day3MissedContentSetup] D3_NR01~03 controller and viewport placeholders configured.");
    }

    private static void ConfigureViewportEffect(
        string prefabName,
        string objectName,
        Vector3 localPosition,
        string objectId,
        string displayName,
        int sortingOrder)
    {
        string path = PrefabRoot + prefabName;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform existing = FindDeepChild(root.transform, objectName);
            GameObject target = existing != null ? existing.gameObject : new GameObject(objectName);
            if (existing == null)
            {
                Transform parent = FindDeepChild(root.transform, "ObjRoot") ?? root.transform;
                target.transform.SetParent(parent, false);
                target.transform.localPosition = localPosition;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = target.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            CCTVSceneObject sceneObject = target.GetComponent<CCTVSceneObject>();
            if (sceneObject == null)
                sceneObject = target.AddComponent<CCTVSceneObject>();

            SerializedObject sceneObjectSo = new SerializedObject(sceneObject);
            sceneObjectSo.FindProperty("objectId").stringValue = objectId;
            sceneObjectSo.FindProperty("targetId").intValue = (int)ReportTargetId.None;
            sceneObjectSo.FindProperty("displayName").stringValue = displayName;
            sceneObjectSo.FindProperty("canBeAnomalyTarget").boolValue = false;
            sceneObjectSo.FindProperty("includeInBaseline").boolValue = true;
            sceneObjectSo.ApplyModifiedPropertiesWithoutUndo();

            target.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureDay3Scene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene setup was cancelled.");
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject contentRoot = FindSceneObject(scene, "Day3ContentRoot");
        if (contentRoot == null)
            throw new InvalidOperationException("Day3ContentRoot was not found in Day3.unity.");

        Day3MissedPresentationController controller = contentRoot.GetComponent<Day3MissedPresentationController>();
        if (controller == null)
            controller = Undo.AddComponent<Day3MissedPresentationController>(contentRoot);

        SerializedObject so = new SerializedObject(controller);
        AssignReference<DayRuntimeController>(so, "dayRuntimeController");
        AssignReference<CCTVAreaView>(so, "areaView");
        AssignReference<CCTVPanController>(so, "panController");
        AssignReference<CCTVTestSceneController>(so, "sceneController");
        AssignReference<FieldModeController>(so, "fieldModeController");
        AssignReference<AnomalyService>(so, "anomalyService");
        AssignReference<CCTVScreenEffectController>(so, "screenEffectController");

        CCTVScreenEffectController screenEffect = UnityEngine.Object.FindFirstObjectByType<CCTVScreenEffectController>(FindObjectsInactive.Include);
        SerializedProperty cameraProperty = so.FindProperty("cctvCamera");
        if (cameraProperty.objectReferenceValue == null && screenEffect != null)
            cameraProperty.objectReferenceValue = screenEffect.WorldCamera;

        ConfigurePresentation(so.FindProperty("serverFace"), "D3_NR01", AreaId.ServerRoom, "", 0.3f, 0.5f, 0.35f, false, false, Vector3.zero, 1);
        ConfigurePresentation(so.FindProperty("ceilingPerson"), "D3_NR02", AreaId.LabCorridor, "OBJ_DORM_CEILING_PERSON_01", 0.25f, 0.8f, 0f, true, true, new Vector3(5f, 0f, 0f), 5);
        ConfigurePresentation(so.FindProperty("reflectionPerson"), "D3_NR03", AreaId.TreatmentRoom, "OBJ_TREAT_REFLECTION_01", 0.3f, 2f, 0f, false, false, Vector3.zero, 0);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigurePresentation(
        SerializedProperty property,
        string eventId,
        AreaId areaId,
        string objectId,
        float probability,
        float duration,
        float delay,
        bool lockInput,
        bool move,
        Vector3 moveOffset,
        int frameSlots)
    {
        property.FindPropertyRelative("eventId").stringValue = eventId;
        property.FindPropertyRelative("areaId").intValue = (int)areaId;
        property.FindPropertyRelative("effectObjectId").stringValue = objectId;
        property.FindPropertyRelative("probability").floatValue = probability;
        property.FindPropertyRelative("duration").floatValue = duration;
        property.FindPropertyRelative("triggerDelay").floatValue = delay;
        property.FindPropertyRelative("viewportMargin").floatValue = 0.05f;
        property.FindPropertyRelative("horizontalCenterTolerance").floatValue = 0.2f;
        property.FindPropertyRelative("lockCctvInput").boolValue = lockInput;
        property.FindPropertyRelative("moveDuringPresentation").boolValue = move;
        property.FindPropertyRelative("movementLocalOffset").vector3Value = moveOffset;
        property.FindPropertyRelative("frameDuration").floatValue = 0.1f;

        SerializedProperty frames = property.FindPropertyRelative("frames");
        if (frameSlots == 0)
            frames.arraySize = 0;
        else if (frames.arraySize < frameSlots)
            frames.arraySize = frameSlots;
    }

    private static void AssignReference<T>(SerializedObject so, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property.objectReferenceValue == null)
            property.objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeepChild(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
