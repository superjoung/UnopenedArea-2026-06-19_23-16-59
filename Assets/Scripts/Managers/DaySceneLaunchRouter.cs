using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 빌드의 첫 씬(현재 Day1)에 배치합니다.
/// 저장된 일차에 맞는 Day 씬으로 즉시 이동합니다.
/// </summary>
public class DaySceneLaunchRouter : MonoBehaviour
{
    [Serializable]
    private struct DaySceneEntry
    {
        [Min(1)] public int day;
        public string sceneName;
    }

    [SerializeField] private DaySceneEntry[] dayScenes;

    private void Awake()
    {
        int savedDay = DayProgressSave.CurrentDay;
        string targetSceneName = FindSceneName(savedDay);
        if (string.IsNullOrWhiteSpace(targetSceneName) ||
            SceneManager.GetActiveScene().name == targetSceneName)
            return;

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning($"[DaySceneLaunchRouter] Day {savedDay} scene is not in Build Settings: {targetSceneName}");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    public void LoadSavedDay()
    {
        string targetSceneName = FindSceneName(DayProgressSave.CurrentDay);
        if (!string.IsNullOrWhiteSpace(targetSceneName))
            SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>개발 중 처음부터 확인할 때 UI Button이나 Inspector 이벤트에 연결합니다.</summary>
    public void ResetProgressAndLoadFirstDay()
    {
        DayProgressSave.ResetProgress();
        LoadSavedDay();
    }

    private string FindSceneName(int day)
    {
        if (dayScenes == null)
            return null;

        foreach (DaySceneEntry entry in dayScenes)
        {
            if (entry.day == day)
                return entry.sceneName;
        }

        return null;
    }
}
