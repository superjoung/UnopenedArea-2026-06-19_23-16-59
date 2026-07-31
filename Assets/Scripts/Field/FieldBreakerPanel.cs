using UnityEngine;

/// <summary>
/// Field-mode breaker interaction. Attach this component to the breaker object,
/// then hold E while standing within range to complete the Day 1 recovery step.
/// </summary>
public class FieldBreakerPanel : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField, Range(1f, 2f)] private float requiredHoldSeconds = 1.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("References (optional)")]
    [SerializeField] private Transform player;
    [SerializeField] private Day1FlowController day1FlowController;

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
    }

    private void OnEnable()
    {
        heldSeconds = 0f;
        IsRestored = false;
        UpdateRange();
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
            return;
        }

        if (Input.GetKey(interactionKey))
        {
            heldSeconds = Mathf.Min(requiredHoldSeconds, heldSeconds + Time.deltaTime);
            HoldProgressChanged?.Invoke(HoldProgress);

            if (heldSeconds >= requiredHoldSeconds)
                RestorePower();

            return;
        }

        if (Input.GetKeyUp(interactionKey))
            ResetHold();
    }

    private void UpdateRange()
    {
        bool wasInRange = IsPlayerInRange;
        IsPlayerInRange = player != null && Vector2.Distance(player.position, transform.position) <= interactionRange;

        if (wasInRange != IsPlayerInRange)
        {
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

    private void RestorePower()
    {
        if (IsRestored)
            return;

        IsRestored = true;
        heldSeconds = requiredHoldSeconds;
        HoldProgressChanged?.Invoke(1f);
        Restored?.Invoke();
        Debug.Log("[FieldBreakerPanel] Power restored.");

        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
