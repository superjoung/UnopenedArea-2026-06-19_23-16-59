using UnityEngine;

/// <summary>
/// BGM과 효과음을 별도 AudioSource로 관리하고, 사용자 볼륨 설정을 유지합니다.
/// 씬의 GameManager 등에 하나만 배치해 사용합니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SoundManager : MonoBehaviour
{
    private const string BgmVolumePreferenceKey = "Sound.BgmVolume";
    private const string SfxVolumePreferenceKey = "Sound.SfxVolume";

    [Header("Audio Sources")]
    [Tooltip("기본 배경음 전용 AudioSource")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("CCTV BGM과 Fail BGM이 공유하는 보조 배경음 AudioSource")]
    [SerializeField] private AudioSource secondaryBgmSource;
    [Tooltip("모든 효과음을 PlayOneShot으로 재생하는 AudioSource")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("대사 타이핑 중에만 루프로 재생하는 전용 AudioSource입니다. 다른 효과음과 겹치지 않게 별도로 할당합니다.")]
    [SerializeField] private AudioSource dialogueTypingSource;

    [Header("Initial BGM")]
    [SerializeField] private AudioClip initialBgm;
    [SerializeField] private bool playInitialBgmOnStart = true;
    [SerializeField] private AudioClip cctvBgm;
    [SerializeField] private AudioClip failBgm;
    [SerializeField, Range(0f, 1f)] private float mainBgmVolumeWhileCctv = 0.25f;
    [Tooltip("전체 BGM 볼륨에 곱해지는 CCTV BGM 개별 비율입니다.")]
    [SerializeField, Range(0f, 1f)] private float cctvBgmVolumeMultiplier = 0.65f;
    [Tooltip("전체 BGM 볼륨에 곱해지는 실패 BGM 개별 비율입니다.")]
    [SerializeField, Range(0f, 1f)] private float failBgmVolumeMultiplier = 1f;

    [Header("Common SFX")]
    [SerializeField] private AudioClip phoneRingSfx;
    [SerializeField] private AudioClip phoneHangupSfx;
    [Tooltip("대사가 한 글자씩 출력되는 동안 반복 재생됩니다.")]
    [SerializeField] private AudioClip dialogueTypingSfx;
    [Tooltip("실패 결과 패널의 '실패' 문구가 표시되는 순간 재생합니다.")]
    [SerializeField] private AudioClip failureResultSfx;
    [Tooltip("최종 실패 손 연출이 화면을 완전히 덮는 순간 재생합니다.")]
    [SerializeField] private AudioClip failureHandCoverSfx;
    [SerializeField] private AudioClip reportPaperSfx;
    [Tooltip("메인룸의 발견 기록/힌트 종이를 펼칠 때 재생합니다.")]
    [SerializeField] private AudioClip foundAnomalyPanelOpenSfx;
    [Tooltip("보고판에서 장소·대상·현상 버튼 및 선택지를 누를 때 재생합니다.")]
    [SerializeField] private AudioClip reportSelectionClickSfx;
    [Tooltip("메인룸 CCTV를 눌러 진입 전환 효과가 시작될 때 재생합니다.")]
    [SerializeField] private AudioClip cctvEntryTransitionSfx;
    [SerializeField] private AudioClip cctvChannelSwitchSfx;
    [SerializeField] private AudioClip correctReportSfx;
    [Tooltip("Day 2에서 그날 첫 정답 보고에만 추가로 재생되는 연출 효과음입니다.")]
    [SerializeField] private AudioClip day2FirstCorrectReportSfx;
    [SerializeField] private AudioClip anomalyAppearedSfx;
    [SerializeField] private AudioClip wrongOrMissedReportSfx;
    [SerializeField] private AudioClip doorOpenSfx;
    [SerializeField] private AudioClip doorCloseSfx;
    [SerializeField] private AudioClip footstepSfxA;
    [SerializeField] private AudioClip footstepSfxB;
    [SerializeField] private AudioClip breakerPowerOnSfx;
    [SerializeField] private AudioClip blackoutFlickerSfx;
    [SerializeField] private AudioClip blackoutImpactSfx;
    [Tooltip("정전 깜빡임 효과음에만 적용되는 개별 볼륨 배율입니다.")]
    [SerializeField, Min(0f)] private float blackoutFlickerVolumeMultiplier = 3f;

    [Header("Default Volume")]
    [SerializeField, Range(0f, 1f)] private float defaultBgmVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;

    public static SoundManager Instance { get; private set; }
    public float BgmVolume { get; private set; }
    public float SfxVolume { get; private set; }
    private bool isCctvBgmMode;
    private bool isFailBgmMode;
    private bool useFirstFootstepClip = true;
    private AudioSource activeDialogueTypingSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 다른 씬의 GameManager 등에 중복으로 붙어도 게임 오브젝트 자체를 지우지 않는다.
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        WarnMissingAudioSources();

        BgmVolume = PlayerPrefs.GetFloat(BgmVolumePreferenceKey, defaultBgmVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumePreferenceKey, defaultSfxVolume);
        ApplyVolumes();
    }

    private void Start()
    {
        if (!playInitialBgmOnStart || bgmSource == null || bgmSource.isPlaying)
            return;

        if (initialBgm != null)
            PlayBgm(initialBgm);
        else if (bgmSource.clip != null)
            bgmSource.Play();
    }

    public void PlayBgm(AudioClip clip, bool restartIfSameClip = false)
    {
        if (clip == null)
            return;

        if (bgmSource == null)
            return;

        if (bgmSource.clip == clip && bgmSource.isPlaying && !restartIfSameClip)
            return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void EnterCctvBgmMode()
    {
        if (isFailBgmMode)
            return;

        isCctvBgmMode = true;
        ApplyBgmVolumes();

        if (cctvBgm != null && secondaryBgmSource != null)
        {
            if (secondaryBgmSource.clip != cctvBgm)
                secondaryBgmSource.clip = cctvBgm;
            if (!secondaryBgmSource.isPlaying)
                secondaryBgmSource.Play();
        }
    }

    public void ExitCctvBgmMode()
    {
        if (isFailBgmMode)
            return;

        isCctvBgmMode = false;
        if (secondaryBgmSource != null)
            secondaryBgmSource.Stop();
        ApplyBgmVolumes();
    }

    public void PlayFailBgm()
    {
        isFailBgmMode = true;
        isCctvBgmMode = false;
        // UnityEngine.Object can represent a destroyed/unassigned serialized
        // reference that is not CLR-null. Null-conditional calls bypass Unity's
        // overloaded null check and can throw during a scene handoff.
        if (bgmSource != null)
            bgmSource.Stop();
        if (secondaryBgmSource != null)
            secondaryBgmSource.Stop();
        if (failBgm == null || secondaryBgmSource == null)
            return;

        secondaryBgmSource.clip = failBgm;
        secondaryBgmSource.loop = true;
        if (!secondaryBgmSource.isPlaying)
            secondaryBgmSource.Play();
    }

    /// <summary>
    /// SoundManager persists across scenes, so retries and manual day selection
    /// must explicitly leave CCTV/failure BGM modes before the next day begins.
    /// Volume preferences are intentionally preserved.
    /// </summary>
    public void ResetForDayLoad()
    {
        isCctvBgmMode = false;
        isFailBgmMode = false;
        useFirstFootstepClip = true;

        if (secondaryBgmSource != null)
            secondaryBgmSource.Stop();
        if (sfxSource != null)
            sfxSource.Stop();
        if (dialogueTypingSource != null)
            dialogueTypingSource.Stop();
        activeDialogueTypingSource = null;

        if (initialBgm != null)
            PlayBgm(initialBgm);
        else if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying)
            bgmSource.Play();

        ApplyVolumes();
    }

    public void PlayPhoneRingSfx()
    {
        if (phoneRingSfx == null || sfxSource == null)
            return;

        sfxSource.clip = phoneRingSfx;
        sfxSource.loop = false;
        sfxSource.Play();
    }

    public void StopPhoneRingAndPlayHangupSfx()
    {
        if (sfxSource == null)
            return;

        // 벨소리가 아직 재생 중이면 즉시 끊은 뒤, 수화기 효과음만 재생한다.
        sfxSource.Stop();
        if (phoneHangupSfx != null)
            sfxSource.PlayOneShot(phoneHangupSfx);
    }

    public void PlayReportPaperSfx() => PlaySfx(reportPaperSfx);
    public void PlayFailureResultSfx() => PlaySfx(failureResultSfx);
    public void PlayFailureHandCoverSfx() => PlaySfx(failureHandCoverSfx);
    public void PlayFoundAnomalyPanelOpenSfx()
    {
        // 전용 클립을 아직 넣지 않은 경우에도 힌트 종이는 기존 종이 펼침음으로 들린다.
        PlaySfx(foundAnomalyPanelOpenSfx != null ? foundAnomalyPanelOpenSfx : reportPaperSfx);
    }
    public void PlayReportSelectionClickSfx() => PlaySfx(reportSelectionClickSfx);
    public void PlayCctvEntryTransitionSfx() => PlaySfx(cctvEntryTransitionSfx);
    public void PlayCctvChannelSwitchSfx() => PlaySfx(cctvChannelSwitchSfx);
    public void PlayCorrectReportSfx() => PlaySfx(correctReportSfx);
    /// <summary>전용 클립이 설정된 경우에만 재생하고, 재생 여부를 반환합니다.</summary>
    public bool PlayDay2FirstCorrectReportSfx()
    {
        if (day2FirstCorrectReportSfx == null)
            return false;

        PlaySfx(day2FirstCorrectReportSfx);
        return true;
    }

    /// <summary>대사 타이핑 시작 시 루프를 시작합니다. 긴 문장도 클립 끝에서 끊기지 않습니다.</summary>
    public void StartDialogueTypingSfx()
    {
        if (dialogueTypingSfx == null)
            return;

        // 전용 소스를 할당하면 다른 SFX와 완전히 분리된다. 아직 미할당 상태에서도
        // 기능 검증은 가능하도록 공용 SFX 소스를 안전한 대체값으로 사용한다.
        AudioSource source = dialogueTypingSource != null ? dialogueTypingSource : sfxSource;
        if (source == null)
            return;

        if (source.isPlaying && source.clip == dialogueTypingSfx)
            return;

        source.Stop();
        source.clip = dialogueTypingSfx;
        source.loop = true;
        source.Play();
        activeDialogueTypingSource = source;
    }

    /// <summary>문장 출력 완료, 즉시 출력, 대화 종료 시 타이핑 루프를 즉시 정지합니다.</summary>
    public void StopDialogueTypingSfx()
    {
        if (activeDialogueTypingSource != null && activeDialogueTypingSource.isPlaying)
            activeDialogueTypingSource.Stop();

        activeDialogueTypingSource = null;
    }
    public void PlayAnomalyAppearedSfx() => PlaySfx(anomalyAppearedSfx);
    public void PlayWrongOrMissedReportSfx() => PlaySfx(wrongOrMissedReportSfx);
    public void PlayDoorOpenSfx() => PlaySfx(doorOpenSfx);
    public void PlayDoorCloseSfx() => PlaySfx(doorCloseSfx);
    public void PlayBreakerPowerOnSfx() => PlaySfx(breakerPowerOnSfx);
    public void PlayBlackoutFlickerSfx() => PlaySfx(blackoutFlickerSfx, blackoutFlickerVolumeMultiplier);
    public void PlayBlackoutImpactSfx() => PlaySfx(blackoutImpactSfx);

    public void PlayNextFootstepSfx()
    {
        AudioClip clip = useFirstFootstepClip ? footstepSfxA : footstepSfxB;
        useFirstFootstepClip = !useFirstFootstepClip;
        PlaySfx(clip);
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        if (sfxSource != null)
            sfxSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
    }

    public void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
        ApplyBgmVolumes();
        SaveVolume(BgmVolumePreferenceKey, BgmVolume);
    }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
            sfxSource.volume = SfxVolume;
        if (dialogueTypingSource != null)
            dialogueTypingSource.volume = SfxVolume;
        SaveVolume(SfxVolumePreferenceKey, SfxVolume);
    }

    private void ApplyVolumes()
    {
        ApplyBgmVolumes();
        if (sfxSource != null)
            sfxSource.volume = SfxVolume;
        if (dialogueTypingSource != null)
            dialogueTypingSource.volume = SfxVolume;
    }

    private void ApplyBgmVolumes()
    {
        if (bgmSource != null)
            bgmSource.volume = BgmVolume * (isCctvBgmMode ? mainBgmVolumeWhileCctv : 1f);
        if (secondaryBgmSource != null)
            secondaryBgmSource.volume = BgmVolume * (isFailBgmMode
                ? failBgmVolumeMultiplier
                : cctvBgmVolumeMultiplier);
    }

    private void WarnMissingAudioSources()
    {
        if (bgmSource == null || secondaryBgmSource == null || sfxSource == null)
            Debug.LogWarning("[SoundManager] BGM, Secondary BGM, SFX AudioSource 3개를 Inspector에 할당하세요.", this);
    }

    private static void SaveVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
