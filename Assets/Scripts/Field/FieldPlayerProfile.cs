using UnityEngine;

[CreateAssetMenu(fileName = "FieldPlayerProfile", menuName = "Unrecorded Area/Field Player Profile")]
public class FieldPlayerProfile : ScriptableObject
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float acceleration = 25f;
    [SerializeField, Min(0f)] private float deceleration = 30f;

    [Header("Aim")]
    [SerializeField, Range(0f, 180f)] private float headRotationLimit = 80f;
    [SerializeField, Min(0.01f)] private float headRotationSmooth = 18f;

    [Header("Vision")]
    [SerializeField, Min(0f)] private float visionRadius = 4f;
    [SerializeField, Range(0f, 360f)] private float visionInnerAngle = 25f;
    [SerializeField, Range(0f, 360f)] private float visionOuterAngle = 55f;
    [SerializeField, Min(0f)] private float visionIntensity = 1.2f;

    [Header("Camera")]
    [SerializeField, Min(0.01f)] private float cameraFollowSmooth = 0.12f;
    [SerializeField] private float cameraXMin = -20f;
    [SerializeField] private float cameraXMax = 20f;

    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float HeadRotationLimit => headRotationLimit;
    public float HeadRotationSmooth => headRotationSmooth;
    public float VisionRadius => visionRadius;
    public float VisionInnerAngle => visionInnerAngle;
    public float VisionOuterAngle => Mathf.Max(visionInnerAngle, visionOuterAngle);
    public float VisionIntensity => visionIntensity;
    public float CameraFollowSmooth => cameraFollowSmooth;
    public float CameraXMin => Mathf.Min(cameraXMin, cameraXMax);
    public float CameraXMax => Mathf.Max(cameraXMin, cameraXMax);
}
