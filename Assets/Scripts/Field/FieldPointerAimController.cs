using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class FieldPointerAimController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FieldPlayerProfile profile;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private InputActionReference pointerAction;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform bodyVisualRoot;
    [SerializeField] private Transform headPivot;
    [SerializeField] private Transform visionPivot;
    [SerializeField] private Light2D visionLight;

    [Header("Action Fallback")]
    [SerializeField] private string pointerActionName = "Pointer";

    [Header("Rotation Offsets")]
    [SerializeField] private float headRotationOffsetDeg;
    [SerializeField] private float visionRotationOffsetDeg = -90f;

    [Header("Options")]
    [SerializeField] private bool flipBodyToPointer = true;
    [SerializeField] private bool clampHeadRotation = true;
    [Tooltip("8방향 상반신 스프라이트를 사용할 때는 회전된 스프라이트가 다시 기울지 않도록 끕니다.")]
    [SerializeField] private bool rotateHeadPivot = true;

    private InputAction resolvedPointerAction;

    public Vector2 PointerScreenPosition { get; private set; }
    public Vector3 PointerWorldPosition { get; private set; }
    public int FacingSign { get; private set; } = 1;
    public float RawPointerAngle { get; private set; }
    public float HeadAngle { get; private set; }
    public float VisionAngle { get; private set; }

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        ResolveAction();
        ApplyVisionProfile();
    }

    private void OnEnable()
    {
        ResolveAction();
        if (resolvedPointerAction != null && !resolvedPointerAction.enabled)
            resolvedPointerAction.Enable();
    }

    private void OnDisable()
    {
        if (resolvedPointerAction != null && resolvedPointerAction.enabled)
            resolvedPointerAction.Disable();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || resolvedPointerAction == null)
            return;

        PointerScreenPosition = resolvedPointerAction.ReadValue<Vector2>();
        Vector3 screenPosition = new Vector3(PointerScreenPosition.x, PointerScreenPosition.y, -targetCamera.transform.position.z);
        PointerWorldPosition = targetCamera.ScreenToWorldPoint(screenPosition);

        Vector2 aimOrigin = visionPivot != null ? visionPivot.position : transform.position;
        Vector2 aimDirection = (Vector2)PointerWorldPosition - aimOrigin;
        if (aimDirection.sqrMagnitude < 0.0001f)
            return;

        RawPointerAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        UpdateFacing(aimDirection.x);
        RotatePivots(RawPointerAngle);
        ApplyVisionProfile();
    }

    private void UpdateFacing(float directionX)
    {
        if (Mathf.Abs(directionX) < 0.001f)
            return;

        FacingSign = directionX >= 0f ? 1 : -1;
        if (!flipBodyToPointer || bodyVisualRoot == null)
            return;

        Vector3 scale = bodyVisualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * FacingSign;
        bodyVisualRoot.localScale = scale;
    }

    private void RotatePivots(float rawPointerAngle)
    {
        HeadAngle = GetHeadAngle(rawPointerAngle) + headRotationOffsetDeg;
        VisionAngle = rawPointerAngle + visionRotationOffsetDeg;

        float smooth = profile != null ? profile.HeadRotationSmooth : 18f;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);

        if (rotateHeadPivot && headPivot != null)
        {
            Quaternion headRotation = Quaternion.Euler(0f, 0f, HeadAngle);
            headPivot.rotation = Quaternion.Slerp(headPivot.rotation, headRotation, t);
        }

        if (visionPivot != null)
        {
            Quaternion visionRotation = Quaternion.Euler(0f, 0f, VisionAngle);
            visionPivot.rotation = Quaternion.Slerp(visionPivot.rotation, visionRotation, t);
        }
    }

    private float GetHeadAngle(float rawPointerAngle)
    {
        if (!clampHeadRotation || profile == null)
            return rawPointerAngle;

        float facingBaseAngle = FacingSign >= 0 ? 0f : 180f;
        float delta = Mathf.DeltaAngle(facingBaseAngle, rawPointerAngle);
        delta = Mathf.Clamp(delta, -profile.HeadRotationLimit, profile.HeadRotationLimit);
        return facingBaseAngle + delta;
    }

    private void ApplyVisionProfile()
    {
        if (profile == null || visionLight == null)
            return;

        visionLight.intensity = profile.VisionIntensity;
        visionLight.pointLightOuterRadius = profile.VisionRadius;
        visionLight.pointLightInnerAngle = profile.VisionInnerAngle;
        visionLight.pointLightOuterAngle = profile.VisionOuterAngle;
    }

    private void ResolveAction()
    {
        if (pointerAction != null && pointerAction.action != null)
        {
            resolvedPointerAction = pointerAction.action;
            return;
        }

        if (playerInput != null && playerInput.actions != null)
            resolvedPointerAction = playerInput.actions.FindAction(pointerActionName, false);

        if (resolvedPointerAction == null)
            Debug.LogWarning($"[FieldPointerAimController] Pointer action is missing. actionName={pointerActionName}", this);
    }
}
