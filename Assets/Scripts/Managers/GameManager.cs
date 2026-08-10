using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public System.Action OpenReport;
    public System.Action<string, string> CCTVAreaChanged;
    public System.Action<float, float> DayTimeChanged;
    public System.Action<int, int> DayWrongReportCountChanged;
    public System.Action<AnomalyRuntime> DayMissedAnomaly;
    public System.Action<AnomalyRuntime> DayCorrectReport;
    public System.Action DayWrongReport;

    public string CurrentCCTVLabel { get; private set; }
    public string CurrentAreaName { get; private set; }
    public int CurrentDayWrongReportCount { get; private set; }
    public int CurrentDayMaxWrongReportCount { get; private set; }

    private bool reportInputEnabled = true;

    public bool ReportInputEnabled => reportInputEnabled;

    private void Update()
    {
        // Global development reset. This must run even while the pause menu is open.
        if (Input.GetKeyDown(KeyCode.F9))
        {
            DaySessionLoader.ResetAllAndLoadDay1();
            return;
        }

        if (PausePanelController.IsPaused)
            return;

        if (reportInputEnabled && Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("[INFO] GameManager::Update - OpenReport Event");

            OpenReport?.Invoke();
        }
    }

    public void SetReportInputEnabled(bool enabled)
    {
        reportInputEnabled = enabled;
    }

    /// <summary>Inspector/UI event용 전체 일차 초기화 진입점입니다.</summary>
    public void ResetAllAndLoadDay1()
    {
        DaySessionLoader.ResetAllAndLoadDay1();
    }

    /// <summary>
    /// Clears scene-bound data before a day scene is loaded. GameManager survives
    /// scene changes, so this prevents terminal-failure state leaking into retries.
    /// </summary>
    public void ResetDayRuntimeState()
    {
        OpenReport = null;
        CCTVAreaChanged = null;
        DayTimeChanged = null;
        DayWrongReportCountChanged = null;
        DayMissedAnomaly = null;
        DayCorrectReport = null;
        DayWrongReport = null;

        CurrentCCTVLabel = string.Empty;
        CurrentAreaName = string.Empty;
        CurrentDayWrongReportCount = 0;
        CurrentDayMaxWrongReportCount = 0;
        reportInputEnabled = true;
    }

    public void NotifyCCTVAreaChanged(string cctvLabel, string areaName)
    {
        CurrentCCTVLabel = cctvLabel;
        CurrentAreaName = areaName;

        CCTVAreaChanged?.Invoke(cctvLabel, areaName);
    }

    public void NotifyDayTimeChanged(float elapsedSec, float durationSec)
    {
        DayTimeChanged?.Invoke(elapsedSec, durationSec);
    }

    public void NotifyDayWrongReportCountChanged(int currentCount, int maxCount)
    {
        CurrentDayWrongReportCount = currentCount;
        CurrentDayMaxWrongReportCount = maxCount;
        DayWrongReportCountChanged?.Invoke(currentCount, maxCount);
    }

    public void NotifyDayMissedAnomaly(AnomalyRuntime runtime)
    {
        DayMissedAnomaly?.Invoke(runtime);
    }

    public void NotifyDayCorrectReport(AnomalyRuntime runtime)
    {
        DayCorrectReport?.Invoke(runtime);
    }

    public void NotifyDayWrongReport()
    {
        DayWrongReport?.Invoke();
    }
}





