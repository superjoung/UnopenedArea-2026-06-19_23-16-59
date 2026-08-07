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

    private CCTVAreaView areaView;
    private CCTVAreaInstance ownerArea;
    private bool playRequested;
    private bool isPlaying;
    private Vector3 glideStartLocalPosition;
    private float glideElapsed;

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
        if (!isPlaying || presentationMode != PresentationMode.GlideHorizontal)
            return;

        float distance = Vector3.Distance(glideStartLocalPosition, glideEndLocalPosition);
        if (distance <= Mathf.Epsilon)
            return;

        glideElapsed += Time.deltaTime;
        float normalized = Mathf.PingPong(glideElapsed * glideUnitsPerSecond / distance, 1f);
        transform.localPosition = Vector3.Lerp(glideStartLocalPosition, glideEndLocalPosition, normalized);
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

        if (targetAnimator != null)
            targetAnimator.Rebind();
    }

    private void HandleAreaShown(CCTVAreaInstance shownArea)
    {
        if (!playRequested || shownArea != ownerArea || startTiming != StartTiming.WhenCctvAreaIsShown)
            return;

        if (!isPlaying || replayWhenAreaIsShown)
            StartNow();
    }

    private void TryStartForCurrentArea()
    {
        if (!playRequested || startTiming != StartTiming.WhenCctvAreaIsShown)
            return;

        if (areaView != null && areaView.CurrentInstance == ownerArea)
            StartNow();
    }

    private void StartNow()
    {
        isPlaying = true;

        switch (presentationMode)
        {
            case PresentationMode.GlideHorizontal:
                glideStartLocalPosition = transform.localPosition;
                glideElapsed = 0f;
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
        }
    }

    private void ResolveReferences()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponent<Animator>();

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
