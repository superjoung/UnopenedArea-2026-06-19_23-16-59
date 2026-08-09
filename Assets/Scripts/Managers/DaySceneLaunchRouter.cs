using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 빌드의 첫 씬(현재 Day1)에 배치합니다.
/// 저장된 일차에 맞는 Day 씬으로 즉시 이동합니다.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class DaySceneLaunchRouter : MonoBehaviour
{
    [Serializable]
    private struct DaySceneEntry
    {
        [Min(1)] public int day;
        public string sceneName;
    }

    [SerializeField] private DaySceneEntry[] dayScenes;

    private static bool editorInitialSceneHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        editorInitialSceneHandled = false;
    }

    private void Awake()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        int savedDay = DayProgressSave.CurrentDay;

        // Only the first scene present when entering Editor Play Mode is authoritative.
        // Scenes loaded later by normal day completion keep their existing transition flags.
        if (Application.isEditor && !editorInitialSceneHandled)
        {
            editorInitialSceneHandled = true;
            int loadedDay = DaySessionLoader.GetLoadedDayOrFallback(savedDay);
            DayProgressSave.SetCurrentDay(loadedDay);
            DayProgressSave.ClearSkipTitleOnNextSceneLoad();
            return;
        }

        editorInitialSceneHandled = true;

        // Saved-progress routing belongs to the launch scene only. Day2/Day3 may
        // be opened directly in the Editor for isolated Play Mode testing.
        string launchSceneName = FindSceneName(1);
        if (!string.Equals(activeSceneName, launchSceneName, StringComparison.OrdinalIgnoreCase))
        {
            int loadedDay = DaySessionLoader.GetLoadedDayOrFallback(savedDay);
            DayProgressSave.SetCurrentDay(loadedDay);
            return;
        }

        string targetSceneName = FindSceneName(savedDay);
        if (string.IsNullOrWhiteSpace(targetSceneName) ||
            string.Equals(activeSceneName, targetSceneName, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning($"[DaySceneLaunchRouter] Day {savedDay} scene is not in Build Settings: {targetSceneName}");
            return;
        }

        LoadDayScene(savedDay, targetSceneName, false);
    }

    public void LoadSavedDay()
    {
        int day = DayProgressSave.CurrentDay;
        string targetSceneName = FindSceneName(day);
        if (!string.IsNullOrWhiteSpace(targetSceneName))
            LoadDayScene(day, targetSceneName, false);
    }

    /// <summary>개발 중 처음부터 확인할 때 UI Button이나 Inspector 이벤트에 연결합니다.</summary>
    public void ResetProgressAndLoadFirstDay()
    {
        DayProgressSave.ResetProgress();
        LoadSavedDay();
    }

    private void LoadDayScene(int day, string sceneName, bool skipTitle)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[DaySceneLaunchRouter] Day {day} scene is not in Build Settings: {sceneName}");
            return;
        }

        DayProgressSave.SetCurrentDay(day);
        if (skipTitle)
            DayProgressSave.RequestSkipTitleOnNextSceneLoad();
        else
            DayProgressSave.ClearSkipTitleOnNextSceneLoad();

        DaySessionLoader.PrepareForSceneLoad();
        SceneManager.LoadScene(sceneName);
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
