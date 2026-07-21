using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class FieldPlayerMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FieldPlayerProfile profile;
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private InputActionReference moveAction;

    [Header("Action Fallback")]
    [SerializeField] private string moveActionName = "Move";

    [Header("Movement Override")]
    [SerializeField] private bool useControllerMoveSpeedOverride;
    [SerializeField, Min(0f)] private float controllerMoveSpeed = 4f;
    [SerializeField, Min(0f)] private float moveSpeedMultiplier = 1f;

    [Header("Rigidbody Smoothing")]
    [SerializeField] private bool configureRigidbodyForSmoothMovement = true;

    [Header("State")]
    [SerializeField] private bool inputEnabled = true;

    private InputAction resolvedMoveAction;

    public Vector2 MoveInput { get; private set; }
    public bool InputEnabled => inputEnabled;
    public float CurrentMoveSpeed => GetMoveSpeed();

    private void Awake()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody2D>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        ConfigureRigidbody();
        ResolveAction();
    }

    private void OnEnable()
    {
        ResolveAction();
        if (resolvedMoveAction != null && !resolvedMoveAction.enabled)
            resolvedMoveAction.Enable();
    }

    private void OnDisable()
    {
        if (resolvedMoveAction != null && resolvedMoveAction.enabled)
            resolvedMoveAction.Disable();
    }

    private void Update()
    {
        MoveInput = inputEnabled && resolvedMoveAction != null
            ? resolvedMoveAction.ReadValue<Vector2>()
            : Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (targetRigidbody == null || profile == null)
            return;

        float targetX = Mathf.Clamp(MoveInput.x, -1f, 1f) * GetMoveSpeed();
        Vector2 velocity = targetRigidbody.linearVelocity;
        float rate = Mathf.Abs(targetX) > 0.01f ? profile.Acceleration : profile.Deceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, rate * Time.fixedDeltaTime);
        targetRigidbody.linearVelocity = velocity;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
            MoveInput = Vector2.zero;
    }

    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetControllerMoveSpeedOverride(bool enabled, float speed)
    {
        useControllerMoveSpeedOverride = enabled;
        controllerMoveSpeed = Mathf.Max(0f, speed);
    }

    private void ConfigureRigidbody()
    {
        if (!configureRigidbodyForSmoothMovement || targetRigidbody == null)
            return;

        targetRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        targetRigidbody.freezeRotation = true;
    }

    private float GetMoveSpeed()
    {
        float baseSpeed = useControllerMoveSpeedOverride
            ? controllerMoveSpeed
            : profile != null ? profile.MoveSpeed : controllerMoveSpeed;

        return baseSpeed * moveSpeedMultiplier;
    }

    private void ResolveAction()
    {
        if (moveAction != null && moveAction.action != null)
        {
            resolvedMoveAction = moveAction.action;
            return;
        }

        if (playerInput != null && playerInput.actions != null)
            resolvedMoveAction = playerInput.actions.FindAction(moveActionName, false);

        if (resolvedMoveAction == null)
            Debug.LogWarning($"[FieldPlayerMovementController] Move action is missing. actionName={moveActionName}", this);
    }
}
