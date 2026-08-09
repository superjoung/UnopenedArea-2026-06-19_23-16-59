using UnityEngine;

/// <summary>
/// 현재 진행 일차만 보관하는 최소 저장소입니다.
/// 플레이 데이터 확장 전까지 PlayerPrefs를 사용합니다.
/// </summary>
public static class DayProgressSave
{
    private const string CurrentDayKey = "UnopenedArea.CurrentDay";
    // 이전 버전에서 재시작용 1회성 값을 PlayerPrefs에 저장할 때 사용한 키입니다.
    // 이제는 비정상 종료 뒤에도 타이틀 생략 상태가 남지 않도록 읽지 않고 정리만 합니다.
    private const string SkipTitleOnNextSceneLoadKey = "UnopenedArea.SkipTitleOnNextSceneLoad";
    // 같은 실행 세션 안에서 씬을 다시 불러올 때만 유지되는 1회성 플래그입니다.
    private static bool skipTitleOnNextSceneLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTransientState()
    {
        skipTitleOnNextSceneLoad = false;
        ClearLegacyPersistedSkipTitleFlag();
    }

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
        ClearLegacyPersistedSkipTitleFlag();
    }

    public static bool ConsumeSkipTitleOnNextSceneLoad()
    {
        bool shouldSkip = skipTitleOnNextSceneLoad;
        skipTitleOnNextSceneLoad = false;
        ClearLegacyPersistedSkipTitleFlag();
        return shouldSkip;
    }

    public static void ClearSkipTitleOnNextSceneLoad()
    {
        skipTitleOnNextSceneLoad = false;
        ClearLegacyPersistedSkipTitleFlag();
    }

    private static void ClearLegacyPersistedSkipTitleFlag()
    {
        if (!PlayerPrefs.HasKey(SkipTitleOnNextSceneLoadKey))
            return;

        PlayerPrefs.DeleteKey(SkipTitleOnNextSceneLoadKey);
        PlayerPrefs.Save();
    }
}
