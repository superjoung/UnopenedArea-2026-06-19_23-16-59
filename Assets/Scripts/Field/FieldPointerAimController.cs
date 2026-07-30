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
    [SerializeField] private FieldPlayerSpriteAnimator spriteAnimator;

    [Header("Flashlight Origin")]
    [Tooltip("VisionPivot의 자식으로 둘 손전등 원점입니다. VisionLight2D는 이 Transform의 자식으로 둡니다.")]
    [SerializeField] private Transform flashlightSocket;
    [Tooltip("0번(오른쪽, 0도)부터 반시계 방향으로 배치한 손전등 위치 기준점 8개입니다. 기준점은 FieldPlayerRoot의 자식으로 둡니다.")]
    [SerializeField] private Transform[] flashlightPositionMarkers = new Transform[8];
    [SerializeField] private float flashlightMarker0AngleDeg;
    [SerializeField, Min(0f)] private float flashlightPositionSmooth = 24f;

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
    private bool loggedInvalidFlashlightHierarchy;

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
        UpdateFlashlightSocket(RawPointerAngle);
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

    /// <summary>
    /// 8방향 손전등 기준점 사이를 각도에 따라 보간한다.
    /// Socket은 VisionPivot의 자식이므로 빛의 회전은 기존 VisionPivot 보간을 그대로 따른다.
    /// 기준점은 플레이어 루트 쪽에 두어, 위치만 손전등 손잡이 궤적을 따라가게 한다.
    /// </summary>
    private void UpdateFlashlightSocket(float rawPointerAngle)
    {
        if (flashlightSocket == null || flashlightPositionMarkers == null || flashlightPositionMarkers.Length < 8)
            return;

        // 기준점이 Socket의 자식이면 Socket 이동과 함께 기준점도 이동해 목표 위치가
        // 계속 멀어지는 자기추적 루프가 된다. 잘못된 계층에서는 보정을 중단한다.
        if (visionPivot == null || flashlightSocket.parent != visionPivot)
        {
            LogInvalidFlashlightHierarchy("FlashlightSocket must be a direct child of VisionPivot.");
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (flashlightPositionMarkers[i] == null)
                return;

            if (flashlightPositionMarkers[i].IsChildOf(flashlightSocket))
            {
                LogInvalidFlashlightHierarchy("Flashlight position markers must not be children of FlashlightSocket.");
                return;
            }
        }

        float framePosition = Mathf.Repeat(rawPointerAngle - flashlightMarker0AngleDeg, 360f) / 45f;
        bool isSpriteFlipped = GetSpriteFacingSign() < 0;

        // FieldPlayerSpriteAnimator와 같은 규칙을 사용한다.
        // 왼쪽 이동 시 look frame은 0<->4, 1<->3, 2 유지 ... 로 바뀐 뒤 X Flip된다.
        // 단순히 현재 좌표만 반사하면 뒤를 보는 상황에서 빛이 등 뒤로 넘어간다.
        if (isSpriteFlipped)
            framePosition = Mathf.Repeat(4f - framePosition, 8f);

        int currentIndex = Mathf.FloorToInt(framePosition) % 8;
        int nextIndex = (currentIndex + 1) % 8;
        float blend = framePosition - Mathf.Floor(framePosition);

        Vector3 targetWorldPosition = Vector3.Lerp(
            flashlightPositionMarkers[currentIndex].position,
            flashlightPositionMarkers[nextIndex].position,
            blend);

        // 선택된 look frame의 기준점을 스프라이트 Flip과 동일하게 반사한다.
        if (isSpriteFlipped)
        {
            Vector3 playerLocalPosition = transform.InverseTransformPoint(targetWorldPosition);
            playerLocalPosition.x = -playerLocalPosition.x;
            targetWorldPosition = transform.TransformPoint(playerLocalPosition);
        }

        // Socket은 VisionPivot의 자식이다. 월드 좌표를 직접 쓰면 회전하는 부모와
        // 위치 보정이 서로 싸울 수 있으므로, 부모 기준 Local Position으로만 이동한다.
        Transform socketParent = flashlightSocket.parent;
        Vector3 targetLocalPosition = socketParent != null
            ? socketParent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        if (flashlightPositionSmooth <= 0f)
        {
            flashlightSocket.localPosition = targetLocalPosition;
            return;
        }

        float t = 1f - Mathf.Exp(-flashlightPositionSmooth * Time.deltaTime);
        flashlightSocket.localPosition = Vector3.Lerp(flashlightSocket.localPosition, targetLocalPosition, t);
    }

    private void LogInvalidFlashlightHierarchy(string reason)
    {
        if (loggedInvalidFlashlightHierarchy)
            return;

        loggedInvalidFlashlightHierarchy = true;
        Debug.LogWarning($"[FieldPointerAimController] Flashlight origin correction is disabled. {reason}", this);
    }

    private int GetSpriteFacingSign()
    {
        if (spriteAnimator == null)
            spriteAnimator = GetComponent<FieldPlayerSpriteAnimator>();

        return spriteAnimator != null ? spriteAnimator.FacingSign : 1;
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
