using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Esc로 여닫는 공용 일시정지 패널입니다.
/// Button OnClick에는 ResumeGame, RestartDay, QuitGame을 각각 연결합니다.
/// </summary>
public class PausePanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("타이틀 대기 중에는 숨길 '하루 다시하기' 버튼 오브젝트입니다.")]
    [SerializeField] private GameObject restartDayButton;
    [SerializeField] private DayTitleController dayTitleController;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private FoundAnomalyPanelController foundAnomalyPanelController;

    [Header("Sound Sliders")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    public static bool IsPaused { get; private set; }
    public bool IsPanelOpen => IsPaused;

    private float previousTimeScale = 1f;

    private void Awake()
    {
        ResolveReferences();
        ConfigureSoundSliders();
        SetPanelVisible(false);
        IsPaused = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePause();
    }

    public void TogglePause()
    {
        if (TryCloseFoundAnomalyPanel())
            return;

        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (IsPaused)
            return;

        ResolveReferences();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        dayRuntimeController?.PauseDay();
        IsPaused = true;

        // 타이틀 화면에서는 패널은 열리지만 재시작할 '하루'가 아직 없으므로 버튼을 숨긴다.
        if (restartDayButton != null)
            restartDayButton.SetActive(dayTitleController == null || !dayTitleController.WaitForPlayerStart);

        SetPanelVisible(true);
        ConfigureSoundSliders();
    }

    public void ResumeGame()
    {
        if (!IsPaused)
            return;

        SetPanelVisible(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        dayRuntimeController?.ResumeDay();
        IsPaused = false;
    }

    /// <summary>현재 Day 씬 전체를 다시 불러옵니다. 모든 런타임 상태가 함께 초기화됩니다.</summary>
    public void RestartDay()
    {
        if (dayTitleController != null && dayTitleController.WaitForPlayerStart)
            return;

        int day = dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null
            ? dayRuntimeController.CurrentDayDefinition.Day
            : DayProgressSave.CurrentDay;
        day = DaySessionLoader.GetLoadedDayOrFallback(day);
        DaySessionLoader.LoadDay(day, true);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        Debug.Log("[PausePanel] Quit requested. Application.Quit runs in a built player.");
#else
        Application.Quit();
#endif
    }

    /// <summary>PausePanel의 BGM Slider On Value Changed에도 직접 연결할 수 있습니다.</summary>
    public void SetBgmVolume(float value)
    {
        SoundManager.Instance?.SetBgmVolume(value);
    }

    /// <summary>PausePanel의 SFX Slider On Value Changed에도 직접 연결할 수 있습니다.</summary>
    public void SetSfxVolume(float value)
    {
        SoundManager.Instance?.SetSfxVolume(value);
    }

    private void SetPanelVisible(bool visible)
    {
        if (pausePanel != null)
            pausePanel.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (dayTitleController == null)
            dayTitleController = FindFirstObjectByType<DayTitleController>(FindObjectsInactive.Include);
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (foundAnomalyPanelController == null)
            foundAnomalyPanelController = FindFirstObjectByType<FoundAnomalyPanelController>(FindObjectsInactive.Include);
    }

    private bool TryCloseFoundAnomalyPanel()
    {
        if (foundAnomalyPanelController == null)
            foundAnomalyPanelController = FindFirstObjectByType<FoundAnomalyPanelController>(FindObjectsInactive.Include);

        if (foundAnomalyPanelController == null || !foundAnomalyPanelController.IsOpen)
            return false;

        foundAnomalyPanelController.ClosePanel();
        return true;
    }

    private void ConfigureSoundSliders()
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null)
            return;

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 1f;
            bgmVolumeSlider.SetValueWithoutNotify(soundManager.BgmVolume);
            bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
            bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.SetValueWithoutNotify(soundManager.SfxVolume);
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }
    }

    private void OnDestroy()
    {
        if (IsPaused)
        {
            Time.timeScale = 1f;
            IsPaused = false;
        }
    }

    public static void ResetGlobalPauseState()
    {
        Time.timeScale = 1f;
        IsPaused = false;
    }
}
