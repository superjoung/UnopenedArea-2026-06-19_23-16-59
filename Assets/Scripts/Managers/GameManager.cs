using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public System.Action OpenReport;
    public System.Action<string, string> CCTVAreaChanged;
    public System.Action<float, float> DayTimeChanged;
    public System.Action<int, int> DayFailureCountChanged;
    public System.Action<AnomalyRuntime> DayMissedAnomaly;
    public System.Action<AnomalyRuntime> DayCorrectReport;
    public System.Action DayWrongReport;

    public string CurrentCCTVLabel { get; private set; }
    public string CurrentAreaName { get; private set; }
    public int CurrentDayFailureCount { get; private set; }
    public int CurrentDayMaxFailureCount { get; private set; }

    private bool reportInputEnabled = true;

    public bool ReportInputEnabled => reportInputEnabled;

    private void Update()
    {
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

    public void NotifyDayFailureCountChanged(int currentCount, int maxCount)
    {
        CurrentDayFailureCount = currentCount;
        CurrentDayMaxFailureCount = maxCount;
        DayFailureCountChanged?.Invoke(currentCount, maxCount);
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





