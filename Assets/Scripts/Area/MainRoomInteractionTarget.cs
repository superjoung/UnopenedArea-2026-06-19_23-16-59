using UnityEngine;
using System.Collections;

public enum MainRoomInteractionType
{
    Phone = 0,
    CCTV = 1,
    Door = 2,
    Report = 3,
    Calendar = 4,
}

[RequireComponent(typeof(Collider2D))]
public class MainRoomInteractionTarget : MonoBehaviour
{
    [SerializeField] private MainRoomInteractionType interactionType;
    [SerializeField] private MainRoomInteractionController interactionController;
    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private GameObject availableVisual;
    [SerializeField] private GameObject outlineVisual;
    [SerializeField] private TransitionEffect transitionEffect;

    [Header("Click Feedback")]
    [SerializeField, Min(1f)] private float clickScaleMultiplier = 1.06f;
    [SerializeField, Min(0.01f)] private float clickGrowDuration = 0.07f;
    [SerializeField, Min(0.01f)] private float clickShrinkDuration = 0.1f;

    public MainRoomInteractionType InteractionType => interactionType;
    public bool IsAvailable { get; private set; }

    private Vector3 defaultLocalScale;
    private Coroutine clickRoutine;

    private void Awake()
    {
        if (targetCollider == null) targetCollider = GetComponent<Collider2D>();
        if (interactionController == null) interactionController = GetComponentInParent<MainRoomInteractionController>();
        if (transitionEffect == null) transitionEffect = FindFirstObjectByType<TransitionEffect>();
        ResolveOutlineVisual();
        defaultLocalScale = transform.localScale;
        SetOutlineVisible(false);
    }

    private void OnMouseUpAsButton()
    {
        if (PausePanelController.IsPaused || DayTitleController.IsBlockingWorldInteractions ||
            clickRoutine != null || interactionController == null || interactionController.InputLocked)
            return;

        clickRoutine = StartCoroutine(PlayClickFeedbackThenInteract());
    }

    private void OnMouseEnter()
    {
        if (!PausePanelController.IsPaused && !DayTitleController.IsBlockingWorldInteractions && CanShowOutline())
            SetOutlineVisible(true);
    }

    private void OnMouseExit()
    {
        if (clickRoutine == null)
            SetOutlineVisible(false);
    }

    private void Update()
    {
        if (outlineVisual == null)
            return;

        // 타이틀/일시정지/전환 중에는 기존 호버 상태도 반드시 숨긴다.
        if (!CanShowOutline())
        {
            SetOutlineVisible(false);
            return;
        }

        // 카메라가 전환된 직후에는 OnMouseEnter가 새로 호출되지 않을 수 있다.
        // 이 경우에만 보조적으로 다시 켠다. 좌표 판정의 일시적인 오차로
        // OnMouseEnter가 켠 아웃라인을 매 프레임 끄지는 않는다.
        if (!outlineVisual.activeSelf && IsPointerOverTarget())
            SetOutlineVisible(true);
    }

    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        // 사용 불가 상태도 나중에 상황 대사를 보여줄 수 있도록 Collider는 항상 켜 둔다.
        if (targetCollider != null) targetCollider.enabled = true;
        if (availableVisual != null) availableVisual.SetActive(available);
        if (!available)
            SetOutlineVisible(false);
    }

    public void ForceHideOutline()
    {
        SetOutlineVisible(false);
    }

    private IEnumerator PlayClickFeedbackThenInteract()
    {
        SetOutlineVisible(true);
        yield return ScaleOverUnscaledTime(defaultLocalScale * clickScaleMultiplier, clickGrowDuration);
        yield return ScaleOverUnscaledTime(defaultLocalScale, clickShrinkDuration);

        clickRoutine = null;
        SetOutlineVisible(false);

        if (IsAvailable)
            interactionController?.TryInteract(interactionType);
        else
            HandleUnavailableInteraction();
    }

    private IEnumerator ScaleOverUnscaledTime(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private void ResolveOutlineVisual()
    {
        if (outlineVisual != null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name == "Outline")
            {
                outlineVisual = child.gameObject;
                return;
            }
        }
    }

    private void SetOutlineVisible(bool visible)
    {
        if (outlineVisual != null && outlineVisual.activeSelf != visible)
            outlineVisual.SetActive(visible);
    }

    private bool CanShowOutline()
    {
        return !PausePanelController.IsPaused &&
               !DayTitleController.IsBlockingWorldInteractions &&
               IsAvailable &&
               clickRoutine == null &&
               (interactionController == null || !interactionController.InputLocked) &&
               (transitionEffect == null || !transitionEffect.IsPlaying);
    }

    private bool IsPointerOverTarget()
    {
        if (targetCollider == null || !targetCollider.enabled)
            return false;

        // 메인룸/현장/CCTV 카메라가 전환되는 구조라 Camera.main이 현재 출력 카메라가 아닐 수 있다.
        // 활성 카메라 기준으로 모두 확인해, 전환 완료 시 이미 올려 둔 마우스도 즉시 감지한다.
        Camera[] activeCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera inputCamera in activeCameras)
        {
            if (inputCamera == null || !inputCamera.enabled)
                continue;

            Vector3 worldPosition = inputCamera.ScreenToWorldPoint(Input.mousePosition);
            if (targetCollider.OverlapPoint(worldPosition))
                return true;
        }

        return false;
    }

    private void HandleUnavailableInteraction()
    {
        // 향후 MainSceneUI/대사 시스템에 "작동하지 않는다", "지금은 때가 아니다" 등을 연결한다.
        Debug.Log($"[MainRoomInteractionTarget] Unavailable interaction clicked. target={interactionType}", this);
    }

    private void OnDisable()
    {
        if (clickRoutine != null)
            StopCoroutine(clickRoutine);

        clickRoutine = null;
        transform.localScale = defaultLocalScale;
        SetOutlineVisible(false);
    }
}
