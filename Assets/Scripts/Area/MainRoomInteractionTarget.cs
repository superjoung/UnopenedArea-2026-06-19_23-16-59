using UnityEngine;
using System.Collections;

public enum MainRoomInteractionType
{
    Phone = 0,
    CCTV = 1,
    Door = 2,
    Report = 3,
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
        if (clickRoutine != null)
            return;

        clickRoutine = StartCoroutine(PlayClickFeedbackThenInteract());
    }

    private void OnMouseEnter()
    {
        if (CanShowOutline())
            SetOutlineVisible(true);
    }

    private void OnMouseExit()
    {
        if (clickRoutine == null)
            SetOutlineVisible(false);
    }

    private void Update()
    {
        if (outlineVisual != null && outlineVisual.activeSelf && !CanShowOutline())
            SetOutlineVisible(false);
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
        return IsAvailable && clickRoutine == null && (transitionEffect == null || !transitionEffect.IsPlaying);
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
