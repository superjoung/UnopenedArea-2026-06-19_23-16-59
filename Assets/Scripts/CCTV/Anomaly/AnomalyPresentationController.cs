using UnityEngine;

/// <summary>
/// 시간에 따른 이상현상 연출을 담당한다.
/// Definition은 이 컴포넌트를 실행만 하고, 실제 움직임/Animator 제어는 프리팹에 둔다.
/// </summary>
public class AnomalyPresentationController : MonoBehaviour
{
    public enum PresentationMode
    {
        None,
        GlideHorizontal,
        AnimatorState,
        SpriteFrames,
        StepRotation,
        LinearMoveToPosition,
    }

    public enum StartTiming
    {
        Immediately,
        WhenCctvAreaIsShown,
    }

    [Header("Start")]
    [Tooltip("같은 오브젝트에 여러 프레젠테이션을 붙일 때 액션에서 지정할 고유 ID입니다.")]
    [SerializeField] private string presentationId;
    [SerializeField] private PresentationMode presentationMode = PresentationMode.None;
    [SerializeField] private StartTiming startTiming = StartTiming.Immediately;
    [Tooltip("대상이 CCTV 화면 안에 연속으로 노출되어야 하는 시간입니다. 0이면 즉시 재생합니다.")]
    [SerializeField, Min(0f)] private float requiredVisibleDuration;
    [Tooltip("CCTV 채널을 다시 볼 때마다 처음부터 재생합니다.")]
    [SerializeField] private bool replayWhenAreaIsShown;

    [Header("Glide Horizontal")]
    [Tooltip("재생을 시작한 현재 위치에서 이 로컬 위치까지 좌우로 왕복합니다.")]
    [SerializeField] private Vector3 glideEndLocalPosition;
    [SerializeField, Min(0.01f)] private float glideUnitsPerSecond = 2f;

    [Header("Animator State")]
    [SerializeField] private Animator targetAnimator;
    [Tooltip("Animator Controller 안의 State 이름입니다. 클립의 Loop Time을 끄면 마지막 프레임에서 유지됩니다.")]
    [SerializeField] private string animatorStateName;
    [SerializeField, Min(0)] private int animatorLayer;
    [SerializeField, Min(0f)] private float animatorCrossFadeDuration;

    [Header("Sprite Frames")]
    [Tooltip("Assign Day3 anomaly frames here. Null slots keep the current sprite.")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private Sprite[] spriteFrames;
    [SerializeField, Min(0.01f)] private float spriteFrameDuration = 0.12f;
    [SerializeField] private bool loopSpriteFrames = true;

    [Header("Step Rotation")]
    [SerializeField] private float stepRotationDegrees = 30f;
    [SerializeField, Min(0.01f)] private float stepRotationInterval = 0.15f;


    private CCTVAreaView areaView;
    private CCTVAreaInstance ownerArea;
    private Camera visibilityCamera;
    private TransitionEffect transitionEffect;
    private bool playRequested;
    private bool isPlaying;
    private float visibleObservationElapsed;
    private Vector3 glideStartLocalPosition;
    private float glideElapsed;
    private float stepRotationElapsed;
    private Quaternion originalLocalRotation;
    private bool originalLocalRotationCached;
    private float spriteFrameElapsed;
    private int spriteFrameIndex;
    private Sprite originalSprite;
    private bool originalSpriteCached;

    public string PresentationId => presentationId;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeAreaView();
        TryStartForCurrentArea();
    }

    private void OnDisable()
    {
        UnsubscribeAreaView();
        StopPresentation();
    }

    private void Update()
    {
        // AreaShown은 채널이 선택된 순간 발생하므로, 긴 맵의 화면 밖 대상은
        // 카메라가 실제로 도달할 때까지 프레젠테이션 시작을 보류한다.
        if (playRequested && !isPlaying && startTiming == StartTiming.WhenCctvAreaIsShown)
            TryStartForCurrentArea(Time.deltaTime);

        if (!isPlaying)
            return;

        if (presentationMode == PresentationMode.GlideHorizontal)
        {
            float distance = Vector3.Distance(glideStartLocalPosition, glideEndLocalPosition);
            if (distance <= Mathf.Epsilon)
                return;

            glideElapsed += Time.deltaTime;
            float normalized = Mathf.PingPong(glideElapsed * glideUnitsPerSecond / distance, 1f);
            transform.localPosition = Vector3.Lerp(glideStartLocalPosition, glideEndLocalPosition, normalized);
            return;
        }

        if (presentationMode == PresentationMode.LinearMoveToPosition)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                glideEndLocalPosition,
                glideUnitsPerSecond * Time.deltaTime);
            return;
        }

        if (presentationMode == PresentationMode.SpriteFrames)
            UpdateSpriteFrames();
        else if (presentationMode == PresentationMode.StepRotation)
            UpdateStepRotation();
    }

    public void PlayPresentation()
    {
        playRequested = true;

        if (startTiming == StartTiming.Immediately)
            StartNow();
        else
            TryStartForCurrentArea();
    }

    public void StopPresentation()
    {
        playRequested = false;
        isPlaying = false;
        glideElapsed = 0f;
        stepRotationElapsed = 0f;
        spriteFrameElapsed = 0f;
        spriteFrameIndex = 0;
        visibleObservationElapsed = 0f;

        if (originalLocalRotationCached)
            transform.localRotation = originalLocalRotation;

        if (targetAnimator != null)
            targetAnimator.Rebind();

        // Sprite 키가 없는 Idle 상태는 Rebind만으로 마지막 애니메이션 프레임을
        // 되돌리지 못한다. Animator 초기화 뒤 활성화 당시의 정상 스프라이트를 복원한다.
        if (originalSpriteCached && targetSpriteRenderer != null)
            targetSpriteRenderer.sprite = originalSprite;
    }

    private void HandleAreaShown(CCTVAreaInstance shownArea)
    {
        if (!playRequested || shownArea != ownerArea || startTiming != StartTiming.WhenCctvAreaIsShown)
            return;

        if (!isPlaying || replayWhenAreaIsShown)
            TryStartForCurrentArea();
    }

    private void TryStartForCurrentArea(float observationDeltaTime = 0f)
    {
        if (!playRequested || startTiming != StartTiming.WhenCctvAreaIsShown)
            return;

        bool isContinuouslyVisible = areaView != null &&
                                     areaView.CurrentInstance == ownerArea &&
                                     !IsAppearanceMaskActive() &&
                                     IsTargetVisibleInCctv();
        if (!isContinuouslyVisible)
        {
            visibleObservationElapsed = 0f;
            return;
        }

        if (requiredVisibleDuration <= 0f)
        {
            StartNow();
            return;
        }

        visibleObservationElapsed += Mathf.Max(0f, observationDeltaTime);
        if (visibleObservationElapsed >= requiredVisibleDuration)
            StartNow();
    }

    private bool IsAppearanceMaskActive()
    {
        if (transitionEffect == null)
            transitionEffect = FindFirstObjectByType<TransitionEffect>();

        return transitionEffect != null && transitionEffect.IsAnomalyBlinkPlaying;
    }

    private bool IsTargetVisibleInCctv()
    {
        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();

        if (visibilityCamera == null)
        {
            CCTVScreenEffectController screenEffect = FindFirstObjectByType<CCTVScreenEffectController>();
            visibilityCamera = screenEffect != null && screenEffect.WorldCamera != null
                ? screenEffect.WorldCamera
                : Camera.main;
        }

        if (targetSpriteRenderer == null || visibilityCamera == null || !targetSpriteRenderer.enabled)
            return false;

        Vector3 viewport = visibilityCamera.WorldToViewportPoint(targetSpriteRenderer.bounds.center);
        return viewport.z > 0f &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }

    private void StartNow()
    {
        isPlaying = true;
        visibleObservationElapsed = 0f;

        switch (presentationMode)
        {
            case PresentationMode.GlideHorizontal:
            case PresentationMode.LinearMoveToPosition:
                glideStartLocalPosition = transform.localPosition;
                glideElapsed = 0f;
                break;

            case PresentationMode.StepRotation:
                if (!originalLocalRotationCached)
                {
                    originalLocalRotation = transform.localRotation;
                    originalLocalRotationCached = true;
                }

                stepRotationElapsed = 0f;
                break;

            case PresentationMode.AnimatorState:
                if (targetAnimator == null || string.IsNullOrWhiteSpace(animatorStateName))
                {
                    Debug.LogWarning($"[AnomalyPresentationController] Animator State mode needs Animator and state name. object={name}");
                    return;
                }

                if (animatorCrossFadeDuration > 0f)
                    targetAnimator.CrossFade(animatorStateName, animatorCrossFadeDuration, animatorLayer, 0f);
                else
                    targetAnimator.Play(animatorStateName, animatorLayer, 0f);
                break;

            case PresentationMode.SpriteFrames:
                if (targetSpriteRenderer == null)
                    targetSpriteRenderer = GetComponent<SpriteRenderer>();

                if (targetSpriteRenderer == null)
                {
                    Debug.LogWarning($"[AnomalyPresentationController] Sprite Frames mode needs a SpriteRenderer. object={name}");
                    isPlaying = false;
                    return;
                }

                if (!originalSpriteCached)
                {
                    originalSprite = targetSpriteRenderer.sprite;
                    originalSpriteCached = true;
                }

                spriteFrameElapsed = 0f;
                spriteFrameIndex = 0;
                ApplySpriteFrame(spriteFrameIndex);
                break;
        }
    }

    private void UpdateStepRotation()
    {
        stepRotationElapsed += Time.deltaTime;
        if (stepRotationElapsed < stepRotationInterval)
            return;

        int stepCount = Mathf.FloorToInt(stepRotationElapsed / stepRotationInterval);
        stepRotationElapsed -= stepCount * stepRotationInterval;

        Vector3 euler = transform.localEulerAngles;
        euler.z += stepRotationDegrees * stepCount;
        transform.localEulerAngles = euler;
    }


    private void UpdateSpriteFrames()
    {
        if (spriteFrames == null || spriteFrames.Length == 0)
            return;

        spriteFrameElapsed += Time.deltaTime;
        if (spriteFrameElapsed < spriteFrameDuration)
            return;

        spriteFrameElapsed %= spriteFrameDuration;
        if (loopSpriteFrames)
            spriteFrameIndex = (spriteFrameIndex + 1) % spriteFrames.Length;
        else
            spriteFrameIndex = Mathf.Min(spriteFrameIndex + 1, spriteFrames.Length - 1);

        ApplySpriteFrame(spriteFrameIndex);
    }

    private void ApplySpriteFrame(int index)
    {
        if (targetSpriteRenderer == null || spriteFrames == null || index < 0 || index >= spriteFrames.Length)
            return;

        Sprite frame = spriteFrames[index];
        if (frame != null)
            targetSpriteRenderer.sprite = frame;
    }

    private void ResolveReferences()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponent<Animator>();

        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();

        // Animator 프레젠테이션도 마지막 프레임에서 멈추므로 최초 정상 모습을 보존한다.
        if (!originalSpriteCached && targetSpriteRenderer != null)
        {
            originalSprite = targetSpriteRenderer.sprite;
            originalSpriteCached = true;
        }

        if (ownerArea == null)
            ownerArea = GetComponentInParent<CCTVAreaInstance>();

        if (areaView == null)
            areaView = FindFirstObjectByType<CCTVAreaView>();
    }

    private void SubscribeAreaView()
    {
        if (areaView != null)
            areaView.AreaShown += HandleAreaShown;
    }

    private void UnsubscribeAreaView()
    {
        if (areaView != null)
            areaView.AreaShown -= HandleAreaShown;
    }
}
