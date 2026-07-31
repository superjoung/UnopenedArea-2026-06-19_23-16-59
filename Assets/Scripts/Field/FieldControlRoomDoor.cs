using UnityEngine;

/// <summary>
/// Day 1 현장 제어실 문입니다.
/// 배전반 복구 후 가까이에서 E를 한 번 누르면 문이 열리고,
/// 열린 문에서 E를 다시 누르면 메인룸으로 복귀합니다.
/// </summary>
public class FieldControlRoomDoor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("Door Visual")]
    [Tooltip("비워두면 이 오브젝트의 SpriteRenderer를 사용합니다.")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [Tooltip("문을 열었을 때 표시할 스프라이트입니다.")]
    [SerializeField] private Sprite openedDoorSprite;

    [Header("References (optional)")]
    [SerializeField] private Transform player;
    [SerializeField] private Day1FlowController day1FlowController;

    public bool IsDoorOpen { get; private set; }

    private Sprite closedDoorSprite;

    private void Awake()
    {
        ResolveReferences();
        CacheClosedDoorSprite();
        SetDoorOpen(false);
    }

    private void Update()
    {
        if (PausePanelController.IsPaused)
            return;

        if (day1FlowController == null)
            ResolveReferences();

        if (day1FlowController == null || day1FlowController.State != Day1FlowState.EmergencyRecovery)
        {
            if (IsDoorOpen)
                SetDoorOpen(false);
            return;
        }

        bool inRange = player != null && Vector2.Distance(player.position, transform.position) <= interactionRange;
        if (!inRange || !Input.GetKeyDown(interactionKey))
            return;

        if (!IsDoorOpen)
        {
            SetDoorOpen(true);
            Debug.Log("[FieldControlRoomDoor] Door opened. Press E again to return to the control room.");
            return;
        }

        Debug.Log("[FieldControlRoomDoor] Returning to control room.");
        day1FlowController.ReturnToControlRoom();
    }

    private void SetDoorOpen(bool open)
    {
        IsDoorOpen = open;
        if (doorRenderer == null)
            return;

        if (open && openedDoorSprite != null)
            doorRenderer.sprite = openedDoorSprite;
        else if (closedDoorSprite != null)
            doorRenderer.sprite = closedDoorSprite;
    }

    private void CacheClosedDoorSprite()
    {
        if (doorRenderer == null)
            return;

        closedDoorSprite = doorRenderer.sprite;
    }

    private void ResolveReferences()
    {
        if (doorRenderer == null)
            doorRenderer = GetComponent<SpriteRenderer>();

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
        Gizmos.color = IsDoorOpen ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
