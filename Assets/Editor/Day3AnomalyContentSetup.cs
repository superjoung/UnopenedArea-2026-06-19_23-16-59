#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Day3AnomalyContentSetup
{
    private const string DefinitionRoot = "Assets/Datas/AnomalyDefinitions/D3/";
    private const string PrefabRoot = "Assets/Resources/Prefabs/AreaPrefabs/";

    private readonly struct ActionSpec
    {
        public readonly AnomalyActionType Type;
        public readonly string TargetObjectId;
        public readonly string PresentationId;
        public readonly Vector3 TargetLocalPosition;

        public ActionSpec(
            AnomalyActionType type,
            string targetObjectId,
            string presentationId = "",
            Vector3 targetLocalPosition = default)
        {
            Type = type;
            TargetObjectId = targetObjectId;
            PresentationId = presentationId;
            TargetLocalPosition = targetLocalPosition;
        }
    }

    [MenuItem("Tools/Day3/Apply Anomaly Placeholder Setup")]
    public static void Apply()
    {
        ConfigureLabCorridor();
        ConfigureTreatmentRoom();
        ConfigureUtilityRoom();
        ConfigureServerRoom();
        ConfigureDefinitions();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Day3AnomalyContentSetup] Day3 anomaly targets, placeholder sprites, and actions configured.");
    }

    private static void ConfigureLabCorridor()
    {
        EditPrefab("Area_LabCorridor.prefab", root =>
        {
            GameObject door = RequireChild(root, "Door");
            EnsureSceneObjectAlias(door, "OBJ_DORM_DOOR_03", ReportTargetId.Door, "D3 Dorm Door", false);
            EnsureSpriteFrames(door, "D3_A03_Frames", 4, 0.18f);

            GameObject wheelchair = RequireChild(root, "WheelChair");
            EnsureSceneObjectAlias(wheelchair, "OBJ_DORM_WHEELCHAIR_02", ReportTargetId.Wheelchair, "D3 Dorm Wheelchair", false);
            EnsureGlide(wheelchair, "D3_A04_Move", new Vector3(2.7f, -1.5f, 0f), 2f);
            EnsureSpriteFrames(wheelchair, "D3_A04_Frames", 4, 0.12f);

            GameObject clockPair = EnsurePlaceholder(
                root,
                "D3_ClockPair_Placeholder",
                new Vector3(0f, 1.6f, 0f),
                "OBJ_DORM_CLOCK_PAIR_01",
                ReportTargetId.Clock,
                "D3 Dorm Clock Pair");

            GameObject chair = RequireChild(root, "Chair");
            EnsureSceneObjectAlias(chair, "OBJ_DORM_CHAIR_03", ReportTargetId.Chair, "D3 Dorm Chair", false);
            EnsureLinearMove(chair, "D3_A11_Move", new Vector3(0f, -1.6f, 0f), 2f);
            RemovePresentation(chair, "D3_A11_Frames");
        });
    }

    private static void ConfigureTreatmentRoom()
    {
        EditPrefab("Area_TreatmentRoom.prefab", root =>
        {
            GameObject clock = RequireChild(root, "Clock");
            EnsureSceneObjectAlias(clock, "OBJ_TREAT_CLOCK_02", ReportTargetId.Clock, "D3 Treatment Clock", false);
            EnsureStepRotation(clock, "D3_A01_StepRotation", 30f, 0.15f);
            RemovePresentation(clock, "D3_A01_Frames");

            GameObject person = RequireChild(root, "Patient1");
            EnsureSceneObjectAlias(person, "OBJ_TREAT_PERSON_03", ReportTargetId.Person, "D3 Treatment Person", false);
            EnsureSpriteFrames(person, "D3_A02_Frames", 4, 0.16f);

            GameObject lightRow = EnsurePlaceholder(
                root,
                "D3_LightRow_Placeholder",
                new Vector3(0f, 2.4f, 0f),
                "OBJ_TREAT_LIGHT_ROW_01",
                ReportTargetId.Light,
                "D3 Treatment Light Row");
            EnsureSpriteFrames(lightRow, "D3_A10_Frames", 4, 0.15f);
        });
    }

    private static void ConfigureUtilityRoom()
    {
        EditPrefab("Area_UtilRoom.prefab", root =>
        {
            GameObject monitor = EnsurePlaceholder(
                root,
                "D3_Monitor_Placeholder",
                new Vector3(0f, 0.5f, 0f),
                "OBJ_UTILITY_MONITOR_01",
                ReportTargetId.Monitor,
                "D3 Utility Monitor");
            EnsureSpriteFrames(monitor, "D3_A05_Frames", 4, 0.12f);

            GameObject toolCart = RequireChild(root, "ToolCart1");
            EnsureSceneObjectAlias(toolCart, "OBJ_UTILITY_TOOLCART_01", ReportTargetId.ToolBox, "D3 Utility Tool Cart", false);
        });
    }

    private static void ConfigureServerRoom()
    {
        EditPrefab("Area_ServerRoom.prefab", root =>
        {
            GameObject serverRack = RequireChild(root, "Server_0_0");
            EnsureSceneObjectAlias(serverRack, "OBJ_SERVER_RACK_01", ReportTargetId.ServerRack, "D3 Server Rack", true);
            EnsureSpriteFrames(serverRack, "D3_A06_Frames", 4, 0.10f);

            GameObject vent = RequireChild(root, "Vent_0_0");
            EnsureSceneObjectAlias(vent, "OBJ_SERVER_VENT_01", ReportTargetId.Vent, "D3 Server Vent", true);
            EnsureSpriteFrames(vent, "D3_A08_Frames", 4, 0.08f);
        });
    }

    private static void ConfigureDefinitions()
    {
        SetActions("D3_A01", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_TREAT_CLOCK_02", "D3_A01_StepRotation"));
        SetActions("D3_A02", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_TREAT_PERSON_03", "D3_A02_Frames"));
        SetActions("D3_A03", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_DORM_DOOR_03", "D3_A03_Frames"));
        SetActions(
            "D3_A04",
            new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_DORM_WHEELCHAIR_02", "D3_A04_Move"),
            new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_DORM_WHEELCHAIR_02", "D3_A04_Frames"));
        SetActions("D3_A05", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_UTILITY_MONITOR_01", "D3_A05_Frames"));
        SetActions("D3_A06", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_SERVER_RACK_01", "D3_A06_Frames"));
        SetActions("D3_A07", new ActionSpec(AnomalyActionType.ChangeSprite, "OBJ_DORM_CLOCK_PAIR_01"));
        SetActions("D3_A08", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_SERVER_VENT_01", "D3_A08_Frames"));
        SetActions(
            "D3_A09",
            new ActionSpec(
                AnomalyActionType.MoveToLocalPosition,
                "OBJ_UTILITY_TOOLCART_01",
                targetLocalPosition: new Vector3(-4.25f, 0.0653f, 0f)));
        SetActions("D3_A10", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_TREAT_LIGHT_ROW_01", "D3_A10_Frames"));
        SetActions("D3_A11", new ActionSpec(AnomalyActionType.PlayPresentation, "OBJ_DORM_CHAIR_03", "D3_A11_Move"));
    }

    private static void EditPrefab(string fileName, Action<GameObject> edit)
    {
        string path = PrefabRoot + fileName;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject RequireChild(GameObject root, string childName)
    {
        Transform child = FindDeepChild(root.transform, childName);
        if (child == null)
            throw new InvalidOperationException($"Missing prefab child. prefab={root.name}, child={childName}");

        return child.gameObject;
    }

    private static GameObject EnsurePlaceholder(
        GameObject root,
        string objectName,
        Vector3 localPosition,
        string objectId,
        ReportTargetId targetId,
        string displayName)
    {
        Transform existing = FindDeepChild(root.transform, objectName);
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            Transform parent = FindDeepChild(root.transform, "ObjRoot") ?? root.transform;
            target = new GameObject(objectName);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;

            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = null;
            renderer.sortingOrder = 10;
        }

        EnsureSceneObjectAlias(target, objectId, targetId, displayName, true);
        return target;
    }

    private static CCTVSceneObject EnsureSceneObjectAlias(
        GameObject target,
        string objectId,
        ReportTargetId targetId,
        string displayName,
        bool reportable)
    {
        CCTVSceneObject sceneObject = null;
        foreach (CCTVSceneObject candidate in target.GetComponents<CCTVSceneObject>())
        {
            SerializedObject candidateSo = new SerializedObject(candidate);
            if (candidateSo.FindProperty("objectId").stringValue == objectId)
            {
                sceneObject = candidate;
                break;
            }
        }

        if (sceneObject == null)
            sceneObject = target.AddComponent<CCTVSceneObject>();

        SerializedObject so = new SerializedObject(sceneObject);
        so.FindProperty("objectId").stringValue = objectId;
        so.FindProperty("targetId").intValue = (int)targetId;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("canBeAnomalyTarget").boolValue = reportable;
        so.FindProperty("includeInBaseline").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sceneObject;
    }

    private static void EnsureSpriteFrames(
        GameObject target,
        string presentationId,
        int frameCount,
        float frameDuration)
    {
        AnomalyPresentationController presentation = FindPresentation(target, presentationId);
        if (presentation == null)
            presentation = target.AddComponent<AnomalyPresentationController>();

        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("presentationId").stringValue = presentationId;
        so.FindProperty("presentationMode").intValue = 3;
        so.FindProperty("startTiming").intValue = 0;
        so.FindProperty("replayWhenAreaIsShown").boolValue = false;
        so.FindProperty("targetSpriteRenderer").objectReferenceValue = target.GetComponent<SpriteRenderer>();

        SerializedProperty frames = so.FindProperty("spriteFrames");
        if (frames.arraySize < frameCount)
            frames.arraySize = frameCount;

        so.FindProperty("spriteFrameDuration").floatValue = frameDuration;
        so.FindProperty("loopSpriteFrames").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureGlide(
        GameObject target,
        string presentationId,
        Vector3 endLocalPosition,
        float unitsPerSecond)
    {
        AnomalyPresentationController presentation = FindPresentation(target, presentationId);
        if (presentation == null)
            presentation = target.AddComponent<AnomalyPresentationController>();

        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("presentationId").stringValue = presentationId;
        so.FindProperty("presentationMode").intValue = 1;
        so.FindProperty("startTiming").intValue = 0;
        so.FindProperty("replayWhenAreaIsShown").boolValue = false;
        so.FindProperty("glideEndLocalPosition").vector3Value = endLocalPosition;
        so.FindProperty("glideUnitsPerSecond").floatValue = unitsPerSecond;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureLinearMove(
        GameObject target,
        string presentationId,
        Vector3 endLocalPosition,
        float unitsPerSecond)
    {
        AnomalyPresentationController presentation = FindPresentation(target, presentationId);
        if (presentation == null)
            presentation = target.AddComponent<AnomalyPresentationController>();

        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("presentationId").stringValue = presentationId;
        so.FindProperty("presentationMode").intValue =
            (int)AnomalyPresentationController.PresentationMode.LinearMoveToPosition;
        so.FindProperty("startTiming").intValue = 0;
        so.FindProperty("replayWhenAreaIsShown").boolValue = false;
        so.FindProperty("glideEndLocalPosition").vector3Value = endLocalPosition;
        so.FindProperty("glideUnitsPerSecond").floatValue = unitsPerSecond;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureStepRotation(
        GameObject target,
        string presentationId,
        float degrees,
        float interval)
    {
        AnomalyPresentationController presentation = FindPresentation(target, presentationId);
        if (presentation == null)
            presentation = target.AddComponent<AnomalyPresentationController>();

        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("presentationId").stringValue = presentationId;
        so.FindProperty("presentationMode").intValue =
            (int)AnomalyPresentationController.PresentationMode.StepRotation;
        so.FindProperty("startTiming").intValue = 0;
        so.FindProperty("replayWhenAreaIsShown").boolValue = false;
        so.FindProperty("stepRotationDegrees").floatValue = degrees;
        so.FindProperty("stepRotationInterval").floatValue = interval;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemovePresentation(GameObject target, string presentationId)
    {
        foreach (AnomalyPresentationController candidate in target.GetComponents<AnomalyPresentationController>())
        {
            SerializedObject so = new SerializedObject(candidate);
            if (so.FindProperty("presentationId").stringValue != presentationId)
                continue;

            UnityEngine.Object.DestroyImmediate(candidate);
            return;
        }
    }



    private static AnomalyPresentationController FindPresentation(GameObject target, string presentationId)
    {
        foreach (AnomalyPresentationController candidate in target.GetComponents<AnomalyPresentationController>())
        {
            SerializedObject so = new SerializedObject(candidate);
            if (so.FindProperty("presentationId").stringValue == presentationId)
                return candidate;
        }

        return null;
    }

    private static void SetActions(string anomalyId, params ActionSpec[] specs)
    {
        string path = DefinitionRoot + anomalyId + ".asset";
        AnomalyDefinition definition = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(path);
        if (definition == null)
            throw new InvalidOperationException($"Missing anomaly definition. path={path}");

        SerializedObject so = new SerializedObject(definition);
        SerializedProperty actions = so.FindProperty("actions");
        UnityEngine.Object[] preservedSprites = new UnityEngine.Object[specs.Length];
        int preservedCount = Mathf.Min(actions.arraySize, specs.Length);
        for (int i = 0; i < preservedCount; i++)
        {
            SerializedProperty existing = actions.GetArrayElementAtIndex(i);
            ActionSpec spec = specs[i];
            if (existing.FindPropertyRelative("actionType").intValue == (int)spec.Type &&
                existing.FindPropertyRelative("targetObjectId").stringValue == spec.TargetObjectId &&
                existing.FindPropertyRelative("presentationId").stringValue == spec.PresentationId)
                preservedSprites[i] = existing.FindPropertyRelative("targetSprite").objectReferenceValue;
        }

        actions.arraySize = specs.Length;

        for (int i = 0; i < specs.Length; i++)
        {
            ActionSpec spec = specs[i];
            SerializedProperty action = actions.GetArrayElementAtIndex(i);
            action.FindPropertyRelative("actionType").intValue = (int)spec.Type;
            action.FindPropertyRelative("targetObjectId").stringValue = spec.TargetObjectId;
            action.FindPropertyRelative("delaySec").floatValue = 0f;
            action.FindPropertyRelative("presentationId").stringValue = spec.PresentationId;
            action.FindPropertyRelative("activeValue").boolValue = false;
            action.FindPropertyRelative("animatorEnabledValue").boolValue = true;
            action.FindPropertyRelative("targetLocalPosition").vector3Value = spec.TargetLocalPosition;
            action.FindPropertyRelative("localPositionOffset").vector3Value = Vector3.zero;
            action.FindPropertyRelative("targetLocalEulerAngles").vector3Value = Vector3.zero;
            action.FindPropertyRelative("targetSprite").objectReferenceValue = preservedSprites[i];
            action.FindPropertyRelative("targetColor").colorValue = Color.white;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
