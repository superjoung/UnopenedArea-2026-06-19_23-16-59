using UnityEngine;

/// <summary>
/// 현재 진행 일차만 보관하는 최소 저장소입니다.
/// 플레이 데이터 확장 전까지 PlayerPrefs를 사용합니다.
/// </summary>
public static class DayProgressSave
{
    private const string CurrentDayKey = "UnopenedArea.CurrentDay";
    private const string SkipTitleOnNextSceneLoadKey = "UnopenedArea.SkipTitleOnNextSceneLoad";
    // 앱을 다시 실행하면 사라지는 1회성 플래그다. 재시작에서만 타이틀 입력 대기를 건너뛴다.
    private static bool skipTitleOnNextSceneLoad;

    public static int CurrentDay => Mathf.Max(1, PlayerPrefs.GetInt(CurrentDayKey, 1));

    public static void SetCurrentDay(int day)
    {
        PlayerPrefs.SetInt(CurrentDayKey, Mathf.Max(1, day));
        PlayerPrefs.Save();
    }

    public static void AdvanceToNextDay(int completedDay)
    {
        SetCurrentDay(Mathf.Max(CurrentDay, completedDay + 1));
    }

    public static void ResetProgress()
    {
        SetCurrentDay(1);
    }

    public static void RequestSkipTitleOnNextSceneLoad()
    {
        skipTitleOnNextSceneLoad = true;
        PlayerPrefs.SetInt(SkipTitleOnNextSceneLoadKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeSkipTitleOnNextSceneLoad()
    {
        bool shouldSkip = skipTitleOnNextSceneLoad || PlayerPrefs.GetInt(SkipTitleOnNextSceneLoadKey, 0) == 1;
        skipTitleOnNextSceneLoad = false;
        PlayerPrefs.DeleteKey(SkipTitleOnNextSceneLoadKey);
        PlayerPrefs.Save();
        return shouldSkip;
    }

    public static void ClearSkipTitleOnNextSceneLoad()
    {
        skipTitleOnNextSceneLoad = false;
        PlayerPrefs.DeleteKey(SkipTitleOnNextSceneLoadKey);
        PlayerPrefs.Save();
    }
}
