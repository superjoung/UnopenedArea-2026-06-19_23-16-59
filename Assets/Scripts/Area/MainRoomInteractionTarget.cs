using UnityEngine;

public enum MainRoomInteractionType
{
    Phone = 0,
    CCTV = 1,
    Door = 2,
}

[RequireComponent(typeof(Collider2D))]
public class MainRoomInteractionTarget : MonoBehaviour
{
    [SerializeField] private MainRoomInteractionType interactionType;
    [SerializeField] private MainRoomInteractionController interactionController;
    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private GameObject availableVisual;

    public MainRoomInteractionType InteractionType => interactionType;
    public bool IsAvailable { get; private set; }

    private void Awake()
    {
        if (targetCollider == null) targetCollider = GetComponent<Collider2D>();
        if (interactionController == null) interactionController = GetComponentInParent<MainRoomInteractionController>();
    }

    private void OnMouseUpAsButton()
    {
        if (IsAvailable) interactionController?.TryInteract(interactionType);
    }

    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        if (targetCollider != null) targetCollider.enabled = available;
        if (availableVisual != null) availableVisual.SetActive(available);
    }
}
