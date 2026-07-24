using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CharacterMoveTest의 필드 테스트 오브젝트를 Day1의 FieldModeRoot 아래로 이식한다.
/// 원본 씬은 저장하지 않으므로 CharacterMoveTest는 변경되지 않는다.
/// </summary>
public static class CharacterMoveFieldImporter
{
    private const string Day1ScenePath = "Assets/Scenes/Day1.unity";
    private const string CharacterMoveScenePath = "Assets/Scenes/CharacterMoveTest.unity";

    private static readonly string[] FieldRootNames =
    {
        "TestBackGround",
        "FieldCamera",
        "FieldPlayerRoot",
        "GlobalLight2D",
    };

    [MenuItem("Tools/Unrecorded Area/Import CharacterMove Field Into Day1")]
    private static void ImportCharacterMoveField()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene day1Scene = EditorSceneManager.OpenScene(Day1ScenePath, OpenSceneMode.Single);
        if (GameObject.Find("FieldModeRoot") != null)
        {
            Debug.LogWarning("[CharacterMoveFieldImporter] FieldModeRoot already exists in Day1. Import skipped.");
            return;
        }

        Scene sourceScene = EditorSceneManager.OpenScene(CharacterMoveScenePath, OpenSceneMode.Additive);
        GameObject fieldModeRoot = new GameObject("FieldModeRoot");
        SceneManager.MoveGameObjectToScene(fieldModeRoot, day1Scene);

        foreach (string rootName in FieldRootNames)
        {
            GameObject sourceRoot = FindRootObject(sourceScene, rootName);
            if (sourceRoot == null)
            {
                Debug.LogError($"[CharacterMoveFieldImporter] Source root is missing: {rootName}");
                Object.DestroyImmediate(fieldModeRoot);
                EditorSceneManager.CloseScene(sourceScene, false);
                return;
            }

            SceneManager.MoveGameObjectToScene(sourceRoot, day1Scene);
            sourceRoot.transform.SetParent(fieldModeRoot.transform, true);
        }

        Camera fieldCamera = fieldModeRoot.GetComponentInChildren<Camera>(true);
        FieldPlayerMovementController fieldPlayer = fieldModeRoot.GetComponentInChildren<FieldPlayerMovementController>(true);
        CCTVTestSceneController cctvController = Object.FindFirstObjectByType<CCTVTestSceneController>();
        CCTVSceneUI cctvUi = Object.FindFirstObjectByType<CCTVSceneUI>();

        GameObject gameManagerObject = GameObject.Find("GameManager");
        if (gameManagerObject == null || fieldCamera == null || fieldPlayer == null || cctvController == null || cctvUi == null)
        {
            Debug.LogError("[CharacterMoveFieldImporter] Day1 references could not be resolved. Import was not saved.");
            Object.DestroyImmediate(fieldModeRoot);
            EditorSceneManager.CloseScene(sourceScene, false);
            return;
        }

        FieldModeController fieldModeController = gameManagerObject.GetComponent<FieldModeController>();
        if (fieldModeController == null)
            fieldModeController = gameManagerObject.AddComponent<FieldModeController>();

        fieldModeController.ConfigureForDay1(
            fieldModeRoot,
            fieldCamera,
            fieldPlayer,
            cctvController.gameObject,
            cctvUi.gameObject);

        fieldModeRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(day1Scene);
        EditorSceneManager.SaveScene(day1Scene);
        EditorSceneManager.CloseScene(sourceScene, false);
        Debug.Log("[CharacterMoveFieldImporter] CharacterMove field imported into Day1. FieldModeRoot starts inactive.");
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
                return rootObject;
        }

        return null;
    }
}
