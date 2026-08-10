using UnityEngine;

/// <summary>
/// 일차를 넘겨도 유지해야 하는 서사 확인 여부를 위한 작은 저장소입니다.
/// 현재는 PlayerPrefs를 사용하며, 새 게임/F9 전체 초기화 시 ClearAll에 관리 중인 플래그를 넘깁니다.
/// </summary>
public static class StoryFlagStore
{
    private const string KeyPrefix = "UnopenedArea.StoryFlag.";

    public static bool Has(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) && PlayerPrefs.GetInt(KeyPrefix + flagId, 0) == 1;
    }

    public static void Set(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return;

        PlayerPrefs.SetInt(KeyPrefix + flagId, 1);
        PlayerPrefs.Save();
    }

    public static void Clear(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return;

        PlayerPrefs.DeleteKey(KeyPrefix + flagId);
        PlayerPrefs.Save();
    }

    public static void ClearAll(params string[] flagIds)
    {
        if (flagIds == null)
            return;

        foreach (string flagId in flagIds)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
                PlayerPrefs.DeleteKey(KeyPrefix + flagId);
        }

        PlayerPrefs.Save();
    }
}
