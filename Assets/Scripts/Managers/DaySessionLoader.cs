using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single entry point for retrying, selecting, or advancing a day.
/// It resets persistent runtime state before replacing the scene.
/// </summary>
public static class DaySessionLoader
{
    /// <summary>
    /// Development reset entry point. Keeps user audio settings but clears all
    /// day/session progress and starts Day 1 from its title state.
    /// </summary>
    public static bool ResetAllAndLoadDay1()
    {
        DayProgressSave.ResetProgress();
        DayProgressSave.ClearSkipTitleOnNextSceneLoad();
        return LoadDay(1, false);
    }

    /// <summary>
    /// Returns the number encoded in the currently loaded Day scene (Day1, Day2...),
    /// falling back to the supplied value for non-day scenes.
    /// </summary>
    public static int GetLoadedDayOrFallback(int fallbackDay)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) &&
            sceneName.StartsWith("Day", System.StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(sceneName.Substring(3), out int parsedDay) && parsedDay > 0)
        {
            return parsedDay;
        }

        return Mathf.Max(1, fallbackDay);
    }

    public static bool LoadDay(int day, bool skipTitle)
    {
        day = Mathf.Max(1, day);
        string sceneName = $"Day{day}";
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[DaySessionLoader] Day scene is not in Build Settings: {sceneName}");
            return false;
        }

        DayProgressSave.SetCurrentDay(day);
        if (skipTitle)
            DayProgressSave.RequestSkipTitleOnNextSceneLoad();
        else
            DayProgressSave.ClearSkipTitleOnNextSceneLoad();

        PrepareForSceneLoad();
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static void PrepareForSceneLoad()
    {
        PausePanelController.ResetGlobalPauseState();
        DayTitleController.ResetGlobalInputBlock();
        GameManager.Instance?.ResetDayRuntimeState();
        SoundManager.Instance?.ResetForDayLoad();
    }
}
