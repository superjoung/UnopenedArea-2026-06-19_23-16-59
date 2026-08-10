using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Field-mode breaker interaction. Attach this component to the breaker object,
/// then hold E while standing within range to complete the Day 1 recovery step.
/// </summary>
public class FieldBreakerPanel : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField, Range(1f, 2f)] private float requiredHoldSeconds = 1.5f;
    [Tooltip("E를 놓았을 때 초당 감소하는 진행도 비율입니다. 1이면 1초에 전체 게이지가 감소합니다.")]
    [SerializeField, Min(0.01f)] private float releaseDecayPerSecond = 1.25f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("References (optional)")]
    [SerializeField] private Transform player;
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private FieldModeController fieldModeController;
    [Tooltip("배전반 자식의 흰색 아웃라인입니다. 비워두면 이름이 Outline인 자식을 자동으로 찾습니다.")]
    [SerializeField] private GameObject outlineVisual;

    [Header("Player Interaction UI (optional)")]
    [Tooltip("플레이어 TopCanvas 아래의 EKey입니다. 비워두면 자동 탐색합니다.")]
    [SerializeField] private GameObject interactionKeyVisual;
    [Tooltip("플레이어 TopCanvas 아래의 BarBack입니다. 비워두면 자동 탐색합니다.")]
    [SerializeField] private GameObject holdGaugeVisual;
    [Tooltip("BarBack 안에서 Fill이라는 이름의 Image를 자동 탐색합니다.")]
    [SerializeField] private Image holdGaugeFill;
    [SerializeField] private Slider holdGaugeSlider;

    public bool IsPlayerInRange { get; private set; }
    public bool IsRestored { get; private set; }
    public float HoldProgress => IsRestored ? 1f : Mathf.Clamp01(heldSeconds / requiredHoldSeconds);

    public System.Action<float> HoldProgressChanged;
    public System.Action<bool> RangeChanged;
    public System.Action Restored;

    private float heldSeconds;

    private void Awake()
    {
        ResolveReferences();
        ResolveOutlineVisual();
        ResolveInteractionUi();
        SetOutlineVisible(false);
        SetInteractionUiVisible(false, false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveOutlineVisual();
        ResolveInteractionUi();
        heldSeconds = 0f;
        IsRestored = false;
        IsPlayerInRange = false;
        SetOutlineVisible(false);
        UpdateRange();
        SetHoldGaugeValue(0f);
    }

    private void Update()
    {
        if (PausePanelController.IsPaused)
            return;

        if (IsRestored)
            return;

        UpdateRange();
        if (!IsPlayerInRange)
        {
            ResetHold();
            RefreshInteractionUi();
            return;
        }

        if (Input.GetKey(interactionKey))
        {
            heldSeconds = Mathf.Min(requiredHoldSeconds, heldSeconds + Time.deltaTime);
            HoldProgressChanged?.Invoke(HoldProgress);

            if (heldSeconds >= requiredHoldSeconds)
                RestorePower();

            RefreshInteractionUi();
            return;
        }

        DecayHold();
        RefreshInteractionUi();
    }

    private void UpdateRange()
    {
        bool wasInRange = IsPlayerInRange;
        IsPlayerInRange = player != null && Vector2.Distance(player.position, transform.position) <= interactionRange;

        if (wasInRange != IsPlayerInRange)
        {
            SetOutlineVisible(IsPlayerInRange && !IsRestored);
            RefreshInteractionUi();
            RangeChanged?.Invoke(IsPlayerInRange);
            Debug.Log(IsPlayerInRange
                ? "[FieldBreakerPanel] In range. Hold E to restore power."
                : "[FieldBreakerPanel] Left breaker interaction range.");
        }
    }

    private void ResetHold()
    {
        if (heldSeconds <= 0f)
            return;

        heldSeconds = 0f;
        HoldProgressChanged?.Invoke(0f);
    }

    private void DecayHold()
    {
        if (heldSeconds <= 0f)
            return;

        float oldProgress = HoldProgress;
        heldSeconds = Mathf.Max(0f, heldSeconds - requiredHoldSeconds * releaseDecayPerSecond * Time.deltaTime);
        if (!Mathf.Approximately(oldProgress, HoldProgress))
            HoldProgressChanged?.Invoke(HoldProgress);
    }

    private void RestorePower()
    {
        if (IsRestored)
            return;

        IsRestored = true;
        SetOutlineVisible(false);
        SetInteractionUiVisible(false, false);
        SoundManager.Instance?.PlayBreakerPowerOnSfx();
        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>();
        fieldModeController?.PlayPowerRestoreLightEffect();
        heldSeconds = requiredHoldSeconds;
        HoldProgressChanged?.Invoke(1f);
        Restored?.Invoke();
        Debug.Log("[FieldBreakerPanel] Power restored.");

        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();

        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>();

        day1FlowController?.CompleteEmergencyObjective();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            FieldPlayerMovementController fieldPlayer = FindFirstObjectByType<FieldPlayerMovementController>(FindObjectsInactive.Include);
            if (fieldPlayer != null)
                player = fieldPlayer.transform;
        }

        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
    }

    private void ResolveInteractionUi()
    {
        if (player == null)
            return;

        if (interactionKeyVisual == null)
        {
            Transform keyTransform = FindChildByName(player, "EKey");
            if (keyTransform != null)
                interactionKeyVisual = keyTransform.gameObject;
        }

        if (holdGaugeVisual == null)
        {
            Transform gaugeTransform = FindChildByName(player, "BarBack");
            if (gaugeTransform != null)
                holdGaugeVisual = gaugeTransform.gameObject;
        }

        if (holdGaugeVisual == null)
            return;

        if (holdGaugeSlider == null)
            holdGaugeSlider = holdGaugeVisual.GetComponentInChildren<Slider>(true);

        if (holdGaugeFill == null)
        {
            Transform fillTransform = FindChildByName(holdGaugeVisual.transform, "Fill");
            if (fillTransform != null)
                holdGaugeFill = fillTransform.GetComponent<Image>();
        }
    }

    private void RefreshInteractionUi()
    {
        bool isHolding = IsPlayerInRange && !IsRestored && Input.GetKey(interactionKey);
        // 키를 놓아도 누적 게이지가 남아 있는 동안에는 감소 과정을 보여 준다.
        bool showGauge = IsPlayerInRange && !IsRestored && (isHolding || heldSeconds > 0f);
        SetInteractionUiVisible(IsPlayerInRange && !IsRestored && !showGauge, showGauge);
        SetHoldGaugeValue(HoldProgress);
    }

    private void SetInteractionUiVisible(bool showKey, bool showGauge)
    {
        if (interactionKeyVisual != null)
            interactionKeyVisual.SetActive(showKey);
        if (holdGaugeVisual != null)
            holdGaugeVisual.SetActive(showGauge);
    }

    private void SetHoldGaugeValue(float value)
    {
        value = Mathf.Clamp01(value);
        if (holdGaugeSlider != null)
            holdGaugeSlider.value = value;
        if (holdGaugeFill != null)
            holdGaugeFill.fillAmount = value;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private void ResolveOutlineVisual()
    {
        if (outlineVisual != null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && string.Equals(child.name, "Outline", System.StringComparison.OrdinalIgnoreCase))
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

    private void OnDisable()
    {
        SetOutlineVisible(false);
        SetInteractionUiVisible(false, false);
        SetHoldGaugeValue(0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
