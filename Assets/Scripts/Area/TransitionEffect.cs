using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum CctvTransitionStyle
{
    ZoomAndFade,
    QuickFade
}

/// <summary>
/// 메인룸→CCTV, 정전→메인룸 화면 전환을 한곳에서 처리합니다.
/// </summary>
public class TransitionEffect : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private Camera mainRoomCamera;
    [SerializeField] private Transform cctvFocusTarget;
    [SerializeField] private CCTVSceneUI cctvSceneUI;

    [Header("Enter CCTV")]
    [SerializeField] private CctvTransitionStyle cctvTransitionStyle = CctvTransitionStyle.ZoomAndFade;
    [SerializeField] private Image cctvEnterPanelImage;
    [SerializeField] private Image toCctvPanel2;
    [SerializeField, Min(0.1f)] private float zoomOrthographicSize = 1.75f;
    [SerializeField, Min(0.05f)] private float zoomDuration = 0.6f;
    [SerializeField] private Ease zoomEase = Ease.InOutQuad;
    [SerializeField, Range(0f, 1f)] private float enterPanelOpaqueAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float fadeStartDuringZoomNormalized = 0.55f;
    [SerializeField, Min(0.05f)] private float enterFadeInDuration = 0.22f;
    [SerializeField, Min(0f)] private float enterHoldOpaqueDuration = 0.08f;
    [SerializeField, Min(0.05f)] private float enterFadeOutDuration = 0.35f;

    [Header("Quick CCTV Fade")]
    [SerializeField, Range(0f, 1f)] private float quickFadeOpaqueAlpha = 1f;
    [SerializeField, Min(0.02f)] private float quickFadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float quickFadeHoldDuration = 0.04f;
    [SerializeField, Min(0.02f)] private float quickFadeOutDuration = 0.18f;

    [Header("Blackout To Main Room")]
    [SerializeField] private Image blackoutPanelImage;
    [SerializeField] private Image blackoutFlashPanelImage;
    [SerializeField, Range(0f, 1f)] private float blackoutFlashPeakAlpha = 0.95f;
    [SerializeField, Min(0.01f)] private float blackoutFlashInDuration = 0.025f;
    [SerializeField, Range(0f, 1f)] private float blackoutOpaqueAlpha = 1f;
    [SerializeField, Range(0, 4)] private int blackoutFlickerCount = 2;
    [SerializeField, Min(0.02f)] private float blackoutFlickerFadeDuration = 0.07f;
    [SerializeField, Min(0f)] private float blackoutGapBeforeFinalFadeDuration = 0.18f;
    [SerializeField, Min(0.02f)] private float finalBlackoutFadeDuration = 0.1f;
    [SerializeField, Min(0f)] private float blackoutHoldDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float mainRoomRevealDuration = 0.55f;
    [SerializeField] private Ease mainRoomRevealEase = Ease.OutQuad;

    [Header("Door To Field")]
    [SerializeField, Min(0.05f)] private float doorFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float doorBlackoutHoldDuration = 0.05f;
    [SerializeField, Min(0.05f)] private float doorFadeOutDuration = 0.35f;

    [Header("Exit CCTV To Main Room")]
    [SerializeField, Min(0.05f)] private float cctvExitFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float cctvExitBlackoutHoldDuration = 0.08f;
    [SerializeField, Min(0.05f)] private float cctvExitFadeOutDuration = 0.3f;

    [Header("Anomaly Appearance Blink")]
    [Tooltip("Eye1Panel. RectTransform의 Bottom 값을 1080 -> 540처럼 줄여 아래로 펼칩니다.")]
    [SerializeField] private RectTransform anomalyEye1Panel;
    [Tooltip("Eye2Panel. RectTransform의 Top 값을 1080 -> 540처럼 줄여 위로 펼칩니다.")]
    [SerializeField] private RectTransform anomalyEye2Panel;
    [SerializeField, Range(1, 3)] private int anomalyQuickBlinkCount = 2;
    [SerializeField, Min(0.02f)] private float anomalyQuickCloseDuration = 0.07f;
    [SerializeField, Min(0.02f)] private float anomalyQuickOpenDuration = 0.07f;
    [SerializeField, Min(0f)] private float anomalyQuickBlinkInterval = 0.06f;
    [SerializeField, Min(0f)] private float anomalyPauseBeforeSlowClose = 0.12f;
    [SerializeField, Min(0.05f)] private float anomalySlowCloseDuration = 0.4f;
    [SerializeField, Min(0.02f)] private float anomalyClosedHoldAfterChange = 0.25f;
    [SerializeField, Min(0.02f)] private float anomalySnapOpenDuration = 0.06f;
    [Tooltip("처음 접힌 Bottom/Top 값에서 닫혔을 때 남길 비율입니다. 0이면 패널이 화면 전체를 덮습니다.")]
    [SerializeField, Range(0f, 0.9f)] private float anomalyClosedInsetRatio = 0f;
    [Tooltip("중앙의 미세한 틈을 없애기 위해 닫힐 때 각 패널을 추가로 겹칠 픽셀 수입니다.")]
    [SerializeField, Min(0f)] private float anomalyCenterOverlapPixels = 240f;
    [SerializeField] private Ease anomalyQuickBlinkEase = Ease.OutQuad;
    [SerializeField] private Ease anomalySlowCloseEase = Ease.InQuad;
    [SerializeField] private Ease anomalySnapOpenEase = Ease.OutQuad;

    [Header("Terminal Failure Hand Cover")]
    [SerializeField] private GameObject failureHandEffectRoot;
    [SerializeField] private RectTransform failureLeftHand;
    [SerializeField] private RectTransform failureRightHand;
    [SerializeField] private Image failureHandBlackoutImage;
    [SerializeField, Min(0.05f)] private float failureHandCloseDuration = 0.85f;
    [SerializeField, Range(0f, 1f)] private float failureHandBlackoutStartNormalized = 0.72f;
    [SerializeField, Min(0f)] private float failureHandClosedHoldDuration = 0.18f;
    [Tooltip("손이 닫히는 전체 시간 중 임팩트 효과음을 재생할 비율입니다. 0=시작, 1=완전히 닫힌 뒤.")]
    [SerializeField, Range(0f, 1f)] private float failureHandImpactTimingNormalized = 0.62f;
    [SerializeField, Min(0.05f)] private float failureBackgroundRevealDuration = 0.55f;
    [SerializeField, Min(0f)] private float failureBackgroundHoldDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float failureHandOffscreenDistanceMultiplier = 0.65f;
    [SerializeField] private Ease failureHandCloseEase = Ease.OutCubic;
    [SerializeField] private Ease failureBackgroundRevealEase = Ease.OutQuad;

    [Header("Result Restart Fade")]
    [SerializeField, Min(0.05f)] private float resultRestartFadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float resultRestartBlackHoldDuration = 0.15f;
    [SerializeField, Min(0.05f)] private float resultRestartFadeInDuration = 0.65f;
    [SerializeField, Min(0f)] private float resultRestartFadeInDelay = 0.1f;

    private Sequence activeSequence;
    private Vector3 defaultCameraPosition;
    private float defaultOrthographicSize;
    private bool cameraDefaultsCached;
    private bool isPlaying;
    private Sequence anomalyBlinkSequence;
    private Vector2 anomalyEye1OpenOffsetMin;
    private Vector2 anomalyEye2OpenOffsetMax;
    private bool anomalyBlinkOffsetsCached;
    private Vector2 failureLeftHandCoveredPosition;
    private Vector2 failureRightHandCoveredPosition;
    private bool failureHandPositionsCached;
    private bool playResultRestartFadeIn;
    private static bool resultRestartFadeInRequested;

    public bool IsPlaying => isPlaying;
    public Camera MainRoomCamera
    {
        get
        {
            ResolveReferences();
            return mainRoomCamera;
        }
    }
    public bool IsAnomalyBlinkPlaying => anomalyBlinkSequence != null && anomalyBlinkSequence.IsActive();
    public float AnomalyBlinkTotalDuration => GetAnomalyBlinkCloseDuration() + anomalyClosedHoldAfterChange + anomalySnapOpenDuration;
    public event Action<bool> PlaybackChanged;

    private void Awake()
    {
        ResolveReferences();
        CacheCameraDefaults();
        SetImageAlpha(cctvEnterPanelImage, 0f);
        SetImageAlpha(toCctvPanel2, 0f);
        playResultRestartFadeIn = resultRestartFadeInRequested;
        resultRestartFadeInRequested = false;
        SetImageAlpha(blackoutPanelImage, playResultRestartFadeIn ? 1f : 0f);
        SetImageAlpha(blackoutFlashPanelImage, 0f);
        if (playResultRestartFadeIn && blackoutPanelImage != null)
        {
            blackoutPanelImage.gameObject.SetActive(true);
            blackoutPanelImage.transform.SetAsLastSibling();
        }
        CacheAnomalyBlinkOpenOffsets();
        SetAnomalyBlinkOpenImmediate();
        CacheFailureHandCoveredPositions();
        ResetFailureHandCoverImmediate();
    }

    private void Start()
    {
        if (!playResultRestartFadeIn || blackoutPanelImage == null)
            return;

        SetPlaying(true);
        activeSequence?.Kill();
        activeSequence = DOTween.Sequence().SetUpdate(true);
        activeSequence.AppendInterval(resultRestartFadeInDelay);
        activeSequence.Append(blackoutPanelImage.DOFade(0f, resultRestartFadeInDuration).SetEase(Ease.OutQuad));
        activeSequence.OnComplete(CompleteSequence);
    }

    private void OnDisable()
    {
        activeSequence?.Kill();
        activeSequence = null;
        SetPlaying(false);
        SetImageAlpha(cctvEnterPanelImage, 0f);
        SetImageAlpha(toCctvPanel2, 0f);
        SetImageAlpha(blackoutPanelImage, 0f);
        SetImageAlpha(blackoutFlashPanelImage, 0f);
        anomalyBlinkSequence?.Kill();
        anomalyBlinkSequence = null;
        SetAnomalyBlinkOpenImmediate();
        ResetFailureHandCoverImmediate();
    }

    public bool TryPlayCctvEntry()
    {
        ResolveReferences();
        if (isPlaying || day1FlowController == null)
            return false;

        Day1FlowState state = day1FlowController.State;
        if (state != Day1FlowState.BaselineReview &&
            state != Day1FlowState.EmergencyRecovery &&
            state != Day1FlowState.Monitoring)
            return false;

        if (cctvTransitionStyle == CctvTransitionStyle.QuickFade && toCctvPanel2 != null)
            return TryPlayQuickCctvEntry();

        if (mainRoomCamera == null || cctvFocusTarget == null || cctvEnterPanelImage == null)
        {
            day1FlowController.EnterCCTVFromMainRoom();
            return true;
        }

        CacheCameraDefaults();
        SetPlaying(true);
        SoundManager.Instance?.PlayCctvEntryTransitionSfx();
        activeSequence?.Kill();
        cctvEnterPanelImage.gameObject.SetActive(true);
        SetImageAlpha(cctvEnterPanelImage, 0f);

        Vector3 focusPosition = new Vector3(cctvFocusTarget.position.x, cctvFocusTarget.position.y, defaultCameraPosition.z);
        activeSequence = DOTween.Sequence();
        activeSequence.Append(mainRoomCamera.transform.DOMove(focusPosition, zoomDuration).SetEase(zoomEase));
        activeSequence.Join(mainRoomCamera.DOOrthoSize(zoomOrthographicSize, zoomDuration).SetEase(zoomEase));
        activeSequence.Insert(zoomDuration * fadeStartDuringZoomNormalized, cctvEnterPanelImage.DOFade(enterPanelOpaqueAlpha, enterFadeInDuration));
        activeSequence.AppendInterval(enterHoldOpaqueDuration);
        activeSequence.AppendCallback(() => day1FlowController.EnterCCTVFromMainRoom());
        activeSequence.AppendInterval(0.05f);
        activeSequence.Append(cctvEnterPanelImage.DOFade(0f, enterFadeOutDuration));
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    public bool TryPlayBlackout(Action onOpaque, Action onCompleted)
    {
        ResolveReferences();
        if (isPlaying || blackoutPanelImage == null || mainRoomCamera == null)
            return false;

        CacheCameraDefaults();
        ForceCloseReportPanel();
        SetPlaying(true);
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        if (blackoutFlashPanelImage != null)
            blackoutFlashPanelImage.gameObject.SetActive(true);
        SetImageAlpha(blackoutPanelImage, 0f);
        SetImageAlpha(blackoutFlashPanelImage, 0f);
        activeSequence = DOTween.Sequence();

        for (int i = 0; i < blackoutFlickerCount; i++)
        {
            activeSequence.AppendCallback(() => SoundManager.Instance?.PlayBlackoutFlickerSfx());
            activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, blackoutFlickerFadeDuration));
            activeSequence.Append(blackoutPanelImage.DOFade(0f, blackoutFlickerFadeDuration));
        }

        activeSequence.AppendInterval(blackoutGapBeforeFinalFadeDuration);

        if (blackoutFlashPanelImage != null)
        {
            activeSequence.AppendCallback(() => SoundManager.Instance?.PlayBlackoutImpactSfx());
            activeSequence.Append(blackoutFlashPanelImage.DOFade(blackoutFlashPeakAlpha, blackoutFlashInDuration));
            activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, finalBlackoutFadeDuration));
            activeSequence.Join(blackoutFlashPanelImage.DOFade(0f, finalBlackoutFadeDuration));
        }
        else
        {
            activeSequence.AppendCallback(() => SoundManager.Instance?.PlayBlackoutImpactSfx());
            activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, finalBlackoutFadeDuration));
        }
        activeSequence.AppendInterval(blackoutHoldDuration);
        activeSequence.AppendCallback(() => onOpaque?.Invoke());
        activeSequence.Append(mainRoomCamera.transform.DOMove(defaultCameraPosition, mainRoomRevealDuration).SetEase(mainRoomRevealEase));
        activeSequence.Join(mainRoomCamera.DOOrthoSize(defaultOrthographicSize, mainRoomRevealDuration).SetEase(mainRoomRevealEase));
        activeSequence.Join(blackoutPanelImage.DOFade(0f, mainRoomRevealDuration).SetEase(mainRoomRevealEase));
        activeSequence.OnComplete(() => { CompleteSequence(); onCompleted?.Invoke(); });
        return true;
    }

    /// <summary>
    /// 메인룸 문을 통한 외부 필드 진입 연출입니다.
    /// 검은 화면이 된 시점에 onOpaque에서 필드를 켜고, 이후 검은 막을 걷습니다.
    /// </summary>
    public bool TryPlayDoorToField(Action onOpaque, Action onCompleted)
    {
        if (isPlaying || blackoutPanelImage == null)
            return false;

        SetPlaying(true);
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        SetImageAlpha(blackoutPanelImage, 0f);

        activeSequence = DOTween.Sequence();
        activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, doorFadeInDuration));
        activeSequence.AppendInterval(doorBlackoutHoldDuration);
        activeSequence.AppendCallback(() => onOpaque?.Invoke());
        activeSequence.Append(blackoutPanelImage.DOFade(0f, doorFadeOutDuration));
        activeSequence.OnComplete(() => { CompleteSequence(); onCompleted?.Invoke(); });
        return true;
    }

    /// <summary>
    /// 일반 감시 중 제어실을 확인하기 위해 CCTV를 나갈 때 사용합니다.
    /// 정전과 달리 깜빡임 없이 검은 패널로 전환만 가립니다.
    /// </summary>
    public bool TryPlayCctvExit(Action onOpaque, float totalDuration = -1f)
    {
        ResolveReferences();
        if (isPlaying)
            return false;

        if (cctvTransitionStyle == CctvTransitionStyle.QuickFade && toCctvPanel2 != null)
            return TryPlayQuickCctvExit(onOpaque);

        if (blackoutPanelImage == null)
            return false;

        CacheCameraDefaults();
        SetPlaying(true);
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        SetImageAlpha(blackoutPanelImage, 0f);

        float durationScale = 1f;
        float baseDuration = cctvExitFadeInDuration + cctvExitBlackoutHoldDuration + cctvExitFadeOutDuration;
        if (totalDuration > 0f && baseDuration > 0f)
            durationScale = totalDuration / baseDuration;

        float fadeInDuration = cctvExitFadeInDuration * durationScale;
        float blackoutHoldDuration = cctvExitBlackoutHoldDuration * durationScale;
        float fadeOutDuration = cctvExitFadeOutDuration * durationScale;

        activeSequence = DOTween.Sequence();
        activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, fadeInDuration));
        activeSequence.AppendInterval(blackoutHoldDuration);
        activeSequence.AppendCallback(() => onOpaque?.Invoke());
        if (mainRoomCamera != null)
        {
            activeSequence.Append(mainRoomCamera.transform
                .DOMove(defaultCameraPosition, fadeOutDuration)
                .SetEase(mainRoomRevealEase));
            activeSequence.Join(mainRoomCamera
                .DOOrthoSize(defaultOrthographicSize, fadeOutDuration)
                .SetEase(mainRoomRevealEase));
            activeSequence.Join(blackoutPanelImage
                .DOFade(0f, fadeOutDuration)
                .SetEase(mainRoomRevealEase));
        }
        else
        {
            activeSequence.Append(blackoutPanelImage.DOFade(0f, fadeOutDuration));
        }
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    private bool TryPlayQuickCctvEntry()
    {
        SetPlaying(true);
        SoundManager.Instance?.PlayCctvEntryTransitionSfx();
        activeSequence?.Kill();
        toCctvPanel2.gameObject.SetActive(true);
        SetImageAlpha(toCctvPanel2, 0f);

        activeSequence = DOTween.Sequence();
        activeSequence.Append(toCctvPanel2.DOFade(quickFadeOpaqueAlpha, quickFadeInDuration));
        activeSequence.AppendInterval(quickFadeHoldDuration);
        activeSequence.AppendCallback(() => day1FlowController.EnterCCTVFromMainRoom());
        activeSequence.Append(toCctvPanel2.DOFade(0f, quickFadeOutDuration));
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    private bool TryPlayQuickCctvExit(Action onOpaque)
    {
        SetPlaying(true);
        activeSequence?.Kill();
        toCctvPanel2.gameObject.SetActive(true);
        SetImageAlpha(toCctvPanel2, 0f);

        activeSequence = DOTween.Sequence();
        activeSequence.Append(toCctvPanel2.DOFade(quickFadeOpaqueAlpha, quickFadeInDuration));
        activeSequence.AppendInterval(quickFadeHoldDuration);
        activeSequence.AppendCallback(() => onOpaque?.Invoke());
        activeSequence.Append(toCctvPanel2.DOFade(0f, quickFadeOutDuration));
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    public bool TryPlayFailureHandCover(Action onCovered, Action onCompleted)
    {
        if (isPlaying || failureHandEffectRoot == null || failureLeftHand == null ||
            failureRightHand == null || failureHandBlackoutImage == null)
            return false;

        CacheFailureHandCoveredPositions();
        SetPlaying(true);
        activeSequence?.Kill();

        failureHandEffectRoot.SetActive(true);
        failureHandEffectRoot.transform.SetAsLastSibling();
        failureHandBlackoutImage.gameObject.SetActive(true);
        failureHandBlackoutImage.transform.SetAsLastSibling();
        SetImageAlpha(failureHandBlackoutImage, 0f);

        float offscreenDistance = GetFailureHandOffscreenDistance();
        Vector2 leftOpenPosition = failureLeftHandCoveredPosition + Vector2.left * offscreenDistance;
        Vector2 rightOpenPosition = failureRightHandCoveredPosition + Vector2.right * offscreenDistance;
        failureLeftHand.anchoredPosition = leftOpenPosition;
        failureRightHand.anchoredPosition = rightOpenPosition;

        float blackoutStart = failureHandCloseDuration * failureHandBlackoutStartNormalized;
        float blackoutDuration = Mathf.Max(0.01f, failureHandCloseDuration - blackoutStart);

        activeSequence = DOTween.Sequence().SetUpdate(true);
        activeSequence.Append(failureLeftHand
            .DOAnchorPos(failureLeftHandCoveredPosition, failureHandCloseDuration)
            .SetEase(failureHandCloseEase));
        activeSequence.Join(failureRightHand
            .DOAnchorPos(failureRightHandCoveredPosition, failureHandCloseDuration)
            .SetEase(failureHandCloseEase));
        activeSequence.Insert(blackoutStart, failureHandBlackoutImage
            .DOFade(1f, blackoutDuration)
            .SetEase(Ease.InQuad));
        activeSequence.InsertCallback(
            failureHandCloseDuration * failureHandImpactTimingNormalized,
            () => SoundManager.Instance?.PlayFailureHandCoverSfx());
        activeSequence.AppendCallback(() =>
        {
            onCovered?.Invoke();
        });
        activeSequence.AppendInterval(failureHandClosedHoldDuration);
        activeSequence.AppendCallback(() =>
        {
            failureLeftHand.anchoredPosition = leftOpenPosition;
            failureRightHand.anchoredPosition = rightOpenPosition;
        });
        activeSequence.Append(failureHandBlackoutImage
            .DOFade(0f, failureBackgroundRevealDuration)
            .SetEase(failureBackgroundRevealEase));
        activeSequence.AppendInterval(failureBackgroundHoldDuration);
        activeSequence.OnComplete(() =>
        {
            ResetFailureHandCoverImmediate();
            CompleteSequence();
            onCompleted?.Invoke();
        });
        return true;
    }

    public bool TryPlayResultRestartFade(Action onOpaque)
    {
        if (isPlaying || blackoutPanelImage == null)
            return false;

        SetPlaying(true);
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        blackoutPanelImage.transform.SetAsLastSibling();
        SetImageAlpha(blackoutPanelImage, 0f);

        activeSequence = DOTween.Sequence().SetUpdate(true);
        activeSequence.Append(blackoutPanelImage.DOFade(1f, resultRestartFadeOutDuration).SetEase(Ease.InQuad));
        activeSequence.AppendInterval(resultRestartBlackHoldDuration);
        activeSequence.AppendCallback(() =>
        {
            resultRestartFadeInRequested = true;
            onOpaque?.Invoke();
        });
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    /// <summary>
    /// Fades the entire screen to black for a scene handoff. Unlike the retry fade,
    /// this does not request title skipping in the destination scene.
    /// </summary>
    public bool TryPlaySceneChangeFade(Action onOpaque)
    {
        if (isPlaying || blackoutPanelImage == null)
            return false;

        SetPlaying(true);
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        blackoutPanelImage.transform.SetAsLastSibling();
        SetImageAlpha(blackoutPanelImage, 0f);

        activeSequence = DOTween.Sequence().SetUpdate(true);
        activeSequence.Append(blackoutPanelImage.DOFade(1f, resultRestartFadeOutDuration).SetEase(Ease.InQuad));
        activeSequence.AppendInterval(resultRestartBlackHoldDuration);
        activeSequence.AppendCallback(() =>
        {
            // The next scene's TransitionEffect starts fully black and fades in,
            // making the scene load invisible to the player.
            resultRestartFadeInRequested = true;
            onOpaque?.Invoke();
        });
        activeSequence.OnComplete(CompleteSequence);
        return true;
    }

    /// <summary>
    /// 이상현상 적용을 가리기 위한 눈꺼풀 연출을 시작하고,
    /// 패널이 완전히 닫히는 시점까지의 시간을 반환합니다.
    /// </summary>
    public float PlayAnomalyAppearanceBlink()
    {
        ForceCloseReportPanel();

        if (anomalyEye1Panel == null || anomalyEye2Panel == null)
            return 0f;

        CacheAnomalyBlinkOpenOffsets();
        anomalyBlinkSequence?.Kill();
        SetAnomalyBlinkOpenImmediate();

        anomalyBlinkSequence = DOTween.Sequence();

        for (int i = 0; i < anomalyQuickBlinkCount; i++)
        {
            AppendAnomalyBlinkPanelInsets(true, anomalyQuickCloseDuration, anomalyQuickBlinkEase);
            AppendAnomalyBlinkPanelInsets(false, anomalyQuickOpenDuration, anomalyQuickBlinkEase);

            if (i < anomalyQuickBlinkCount - 1)
                anomalyBlinkSequence.AppendInterval(anomalyQuickBlinkInterval);
        }

        anomalyBlinkSequence.AppendInterval(anomalyPauseBeforeSlowClose);
        AppendAnomalyBlinkPanelInsets(true, anomalySlowCloseDuration, anomalySlowCloseEase);

        float applyAfter = GetAnomalyBlinkCloseDuration();

        anomalyBlinkSequence.AppendInterval(anomalyClosedHoldAfterChange);
        AppendAnomalyBlinkPanelInsets(false, anomalySnapOpenDuration, anomalySnapOpenEase);
        anomalyBlinkSequence.OnComplete(() =>
        {
            anomalyBlinkSequence = null;
            SetAnomalyBlinkOpenImmediate();
        });

        return applyAfter;
    }

    public void CancelAnomalyAppearanceBlink()
    {
        anomalyBlinkSequence?.Kill();
        anomalyBlinkSequence = null;
        CacheAnomalyBlinkOpenOffsets();
        SetAnomalyBlinkOpenImmediate();
    }

    private float GetAnomalyBlinkCloseDuration()
    {
        return anomalyQuickBlinkCount * (anomalyQuickCloseDuration + anomalyQuickOpenDuration)
            + (anomalyQuickBlinkCount - 1) * anomalyQuickBlinkInterval
            + anomalyPauseBeforeSlowClose + anomalySlowCloseDuration;
    }

    private void CompleteSequence()
    {
        activeSequence = null;
        SetPlaying(false);
    }

    private void SetPlaying(bool playing)
    {
        if (isPlaying == playing)
            return;

        isPlaying = playing;
        PlaybackChanged?.Invoke(playing);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void CacheAnomalyBlinkOpenOffsets()
    {
        if (anomalyBlinkOffsetsCached)
            return;

        if (anomalyEye1Panel != null)
            anomalyEye1OpenOffsetMin = anomalyEye1Panel.offsetMin;
        if (anomalyEye2Panel != null)
            anomalyEye2OpenOffsetMax = anomalyEye2Panel.offsetMax;
        anomalyBlinkOffsetsCached = true;
    }

    private void AppendAnomalyBlinkPanelInsets(bool closed, float duration, Ease ease)
    {
        Vector2 eye1Target = anomalyEye1OpenOffsetMin;
        Vector2 eye2Target = anomalyEye2OpenOffsetMax;
        if (closed)
        {
            eye1Target.y *= anomalyClosedInsetRatio;
            eye2Target.y *= anomalyClosedInsetRatio;
            eye1Target.y -= anomalyCenterOverlapPixels;
            eye2Target.y += anomalyCenterOverlapPixels;
        }

        anomalyBlinkSequence.Append(DOTween.To(
            () => anomalyEye1Panel.offsetMin,
            value => anomalyEye1Panel.offsetMin = value,
            eye1Target,
            duration).SetEase(ease));
        anomalyBlinkSequence.Join(DOTween.To(
            () => anomalyEye2Panel.offsetMax,
            value => anomalyEye2Panel.offsetMax = value,
            eye2Target,
            duration).SetEase(ease));
    }

    private void SetAnomalyBlinkOpenImmediate()
    {
        if (!anomalyBlinkOffsetsCached)
            return;

        if (anomalyEye1Panel != null)
            anomalyEye1Panel.offsetMin = anomalyEye1OpenOffsetMin;
        if (anomalyEye2Panel != null)
            anomalyEye2Panel.offsetMax = anomalyEye2OpenOffsetMax;
    }

    private void CacheFailureHandCoveredPositions()
    {
        if (failureHandPositionsCached || failureLeftHand == null || failureRightHand == null)
            return;

        failureLeftHandCoveredPosition = failureLeftHand.anchoredPosition;
        failureRightHandCoveredPosition = failureRightHand.anchoredPosition;
        failureHandPositionsCached = true;
    }

    private float GetFailureHandOffscreenDistance()
    {
        RectTransform effectRootRect = failureHandEffectRoot != null
            ? failureHandEffectRoot.transform as RectTransform
            : null;
        float canvasWidth = effectRootRect != null ? effectRootRect.rect.width : 0f;
        float handWidth = Mathf.Max(failureLeftHand.rect.width, failureRightHand.rect.width);
        return Mathf.Max(canvasWidth, Screen.width) * failureHandOffscreenDistanceMultiplier + handWidth * 0.5f;
    }

    private void ResetFailureHandCoverImmediate()
    {
        if (failureHandPositionsCached)
        {
            if (failureLeftHand != null)
                failureLeftHand.anchoredPosition = failureLeftHandCoveredPosition;
            if (failureRightHand != null)
                failureRightHand.anchoredPosition = failureRightHandCoveredPosition;
        }

        SetImageAlpha(failureHandBlackoutImage, 0f);
        if (failureHandEffectRoot != null)
            failureHandEffectRoot.SetActive(false);
    }

    private void CacheCameraDefaults()
    {
        if (cameraDefaultsCached || mainRoomCamera == null) return;
        defaultCameraPosition = mainRoomCamera.transform.position;
        defaultOrthographicSize = mainRoomCamera.orthographicSize;
        cameraDefaultsCached = true;
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null) day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (cctvSceneUI == null) cctvSceneUI = FindFirstObjectByType<CCTVSceneUI>();
        if (mainRoomCamera == null)
        {
            GameObject cameraObject = GameObject.Find("MainCamera");
            if (cameraObject != null) mainRoomCamera = cameraObject.GetComponent<Camera>();
        }

        if (cctvFocusTarget == null)
        {
            foreach (MainRoomInteractionTarget target in FindObjectsByType<MainRoomInteractionTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target != null && target.InteractionType == MainRoomInteractionType.CCTV)
                {
                    cctvFocusTarget = target.transform;
                    break;
                }
            }
        }
    }

    private void ForceCloseReportPanel()
    {
        if (cctvSceneUI == null)
            cctvSceneUI = FindFirstObjectByType<CCTVSceneUI>();

        cctvSceneUI?.ForceCloseReportPanel();
    }
}
