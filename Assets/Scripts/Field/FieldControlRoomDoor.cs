using UnityEngine;

/// <summary>
/// Attach to the field-side door or return point. After the breaker is fixed,
/// holding E near this object returns the player to the control-room CCTV mode.
/// </summary>
public class FieldControlRoomDoor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField, Min(0f)] private float requiredHoldSeconds = 0.35f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("References (optional)")]
    [SerializeField] private Transform player;
    [SerializeField] private Day1FlowController day1FlowController;

    private float heldSeconds;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (day1FlowController == null)
            ResolveReferences();

        if (day1FlowController == null || day1FlowController.State != Day1FlowState.EmergencyRecovery)
        {
            heldSeconds = 0f;
            return;
        }

        bool inRange = player != null && Vector2.Distance(player.position, transform.position) <= interactionRange;
        if (!inRange || !Input.GetKey(interactionKey))
        {
            heldSeconds = 0f;
            return;
        }

        heldSeconds += Time.deltaTime;
        if (heldSeconds < requiredHoldSeconds)
            return;

        heldSeconds = 0f;
        Debug.Log("[FieldControlRoomDoor] Returning to control room.");
        day1FlowController.ReturnToControlRoom();
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
