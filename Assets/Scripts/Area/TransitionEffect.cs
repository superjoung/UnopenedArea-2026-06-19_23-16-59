using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인룸→CCTV, 정전→메인룸 화면 전환을 한곳에서 처리합니다.
/// </summary>
public class TransitionEffect : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private Camera mainRoomCamera;
    [SerializeField] private Transform cctvFocusTarget;

    [Header("Enter CCTV")]
    [SerializeField] private Image cctvEnterPanelImage;
    [SerializeField, Min(0.1f)] private float zoomOrthographicSize = 1.75f;
    [SerializeField, Min(0.05f)] private float zoomDuration = 0.6f;
    [SerializeField] private Ease zoomEase = Ease.InOutQuad;
    [SerializeField, Range(0f, 1f)] private float enterPanelOpaqueAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float fadeStartDuringZoomNormalized = 0.55f;
    [SerializeField, Min(0.05f)] private float enterFadeInDuration = 0.22f;
    [SerializeField, Min(0f)] private float enterHoldOpaqueDuration = 0.08f;
    [SerializeField, Min(0.05f)] private float enterFadeOutDuration = 0.35f;

    [Header("Blackout To Main Room")]
    [SerializeField] private Image blackoutPanelImage;
    [SerializeField, Range(0f, 1f)] private float blackoutOpaqueAlpha = 1f;
    [SerializeField, Range(1, 4)] private int blackoutFlickerCount = 2;
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

    private Sequence activeSequence;
    private Vector3 defaultCameraPosition;
    private float defaultOrthographicSize;
    private bool cameraDefaultsCached;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        ResolveReferences();
        CacheCameraDefaults();
        SetImageAlpha(cctvEnterPanelImage, 0f);
        SetImageAlpha(blackoutPanelImage, 0f);
    }

    private void OnDisable()
    {
        activeSequence?.Kill();
        activeSequence = null;
        isPlaying = false;
        SetImageAlpha(cctvEnterPanelImage, 0f);
        SetImageAlpha(blackoutPanelImage, 0f);
    }

    public bool TryPlayCctvEntry()
    {
        ResolveReferences();
        if (isPlaying || day1FlowController == null)
            return false;

        Day1FlowState state = day1FlowController.State;
        if (state != Day1FlowState.BaselineReview && state != Day1FlowState.EmergencyRecovery)
            return false;

        if (mainRoomCamera == null || cctvFocusTarget == null || cctvEnterPanelImage == null)
        {
            day1FlowController.EnterCCTVFromMainRoom();
            return true;
        }

        CacheCameraDefaults();
        isPlaying = true;
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
        isPlaying = true;
        activeSequence?.Kill();
        blackoutPanelImage.gameObject.SetActive(true);
        SetImageAlpha(blackoutPanelImage, 0f);
        activeSequence = DOTween.Sequence();

        for (int i = 0; i < blackoutFlickerCount; i++)
        {
            activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, blackoutFlickerFadeDuration));
            activeSequence.Append(blackoutPanelImage.DOFade(0f, blackoutFlickerFadeDuration));
        }

        activeSequence.AppendInterval(blackoutGapBeforeFinalFadeDuration);
        activeSequence.Append(blackoutPanelImage.DOFade(blackoutOpaqueAlpha, finalBlackoutFadeDuration));
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

        isPlaying = true;
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

    private void CompleteSequence()
    {
        activeSequence = null;
        isPlaying = false;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
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
}
