using UnityEngine;

/// <summary>
/// 필드 플레이어의 하반신 걷기와 상반신 8방향 시선 스프라이트를 제어합니다.
/// 물리/손전등 축이 있는 FieldPlayerRoot는 반전하지 않습니다.
/// 상체와 하체는 이동 방향에 맞춰 함께 반전하되,
/// 왼쪽일 때 상체 look 프레임은 좌우 반사 대응 프레임으로 교체합니다.
/// </summary>
public class FieldPlayerSpriteAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FieldPlayerMovementController movementController;
    [SerializeField] private FieldPointerAimController pointerAimController;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer headRenderer;

    [Header("Body Sprites")]
    [SerializeField] private Sprite idleBodySprite;
    [SerializeField] private Sprite[] walkBodySprites;
    [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 10f;
    [SerializeField, Min(0.001f)] private float movementThreshold = 0.01f;

    [Header("Head Look Sprites")]
    [Tooltip("0번은 정면, 이후 프레임은 반시계 방향으로 45도씩 배치합니다.")]
    [SerializeField] private Sprite[] lookHeadSprites;
    [Tooltip("0번(정면) 스프라이트가 바라보는 월드 각도입니다. 기본 0은 오른쪽입니다.")]
    [SerializeField] private float lookFrame0AngleDeg;
    [SerializeField] private bool lookFramesCounterClockwise = true;

    private float walkElapsed;
    private int facingSign = 1;

    /// <summary>이동 방향에 따라 현재 스프라이트가 향하는 좌우 방향입니다. 오른쪽은 1, 왼쪽은 -1입니다.</summary>
    public int FacingSign => facingSign;

    private void Awake()
    {
        ResolveReferences();
        ApplySprites(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplySprites(false);
    }

    private void Update()
    {
        ResolveReferences();
        bool isMoving = movementController != null && Mathf.Abs(movementController.MoveInput.x) > movementThreshold;

        UpdateFacing(isMoving);
        if (isMoving && walkBodySprites != null && walkBodySprites.Length > 0)
            walkElapsed += Time.deltaTime;
        else
            walkElapsed = 0f;

        ApplySprites(isMoving);
    }

    private void UpdateFacing(bool isMoving)
    {
        if (isMoving && movementController != null)
            facingSign = movementController.MoveInput.x >= 0f ? 1 : -1;
        if (bodyRenderer != null)
            bodyRenderer.flipX = facingSign < 0;
        if (headRenderer != null)
            headRenderer.flipX = facingSign < 0;
    }

    private void ApplySprites(bool isMoving)
    {
        if (bodyRenderer != null)
        {
            if (isMoving && walkBodySprites != null && walkBodySprites.Length > 0)
            {
                int frame = Mathf.FloorToInt(walkElapsed * walkFramesPerSecond) % walkBodySprites.Length;
                bodyRenderer.sprite = walkBodySprites[frame];
            }
            else if (idleBodySprite != null)
            {
                bodyRenderer.sprite = idleBodySprite;
            }
        }

        if (headRenderer != null && lookHeadSprites != null && lookHeadSprites.Length > 0)
        {
            float angle = pointerAimController != null ? pointerAimController.RawPointerAngle : lookFrame0AngleDeg;
            float relativeAngle = Mathf.Repeat(angle - lookFrame0AngleDeg, 360f);
            float signedDirection = lookFramesCounterClockwise ? 1f : -1f;
            int frame = Mathf.RoundToInt(relativeAngle / 45f * signedDirection);
            frame = (frame % lookHeadSprites.Length + lookHeadSprites.Length) % lookHeadSprites.Length;

            // 왼쪽을 향하면 두 레이어를 X flip하고, 시선 방향은 반사 대응 프레임으로 바꾼다.
            // 0<->4, 1<->3, 2 유지, 5<->7, 6 유지
            if (facingSign < 0)
                frame = GetFlippedLookFrame(frame);

            headRenderer.sprite = lookHeadSprites[frame];
        }
    }

    private static int GetFlippedLookFrame(int frame)
    {
        switch (frame)
        {
            case 0: return 4;
            case 1: return 3;
            case 2: return 2;
            case 3: return 1;
            case 4: return 0;
            case 5: return 7;
            case 6: return 6;
            case 7: return 5;
            default: return frame;
        }
    }

    private void ResolveReferences()
    {
        if (movementController == null)
            movementController = GetComponent<FieldPlayerMovementController>();
        if (pointerAimController == null)
            pointerAimController = GetComponent<FieldPointerAimController>();
    }
}
