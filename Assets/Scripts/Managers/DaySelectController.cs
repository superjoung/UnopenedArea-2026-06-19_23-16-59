using UnityEngine;

/// <summary>
/// Attach to a debug/menu canvas. Button OnClick can call LoadDay1/2/3.
/// Day selection shows that day's title; restart paths use DaySessionLoader directly.
/// </summary>
public class DaySelectController : MonoBehaviour
{
    [SerializeField] private bool skipTitleWhenSelectingDay;

    public void LoadDay1() => LoadDay(1);
    public void LoadDay2() => LoadDay(2);
    public void LoadDay3() => LoadDay(3);
    public void ResetAllAndLoadDay1() => DaySessionLoader.ResetAllAndLoadDay1();

    public void LoadDay(int day)
    {
        DaySessionLoader.LoadDay(day, skipTitleWhenSelectingDay);
    }
}
